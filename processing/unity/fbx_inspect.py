#!/usr/bin/env python3
"""Dependency-free reader for the binary FBX files in this repository.

Unity is not available in every environment (it needs a licence and a multi-GB
install), but the question "does this skeleton actually contain the bones a
Humanoid Avatar needs, and does the committed clip metadata match the frames
that are really in the FBX?" can be answered from the bytes on disk. This module
is deliberately small and has no third-party dependencies so it runs in CI.

It reads FBX 7.x **binary** nodes (both the 32-bit-offset 7400 files used by
the two base characters and the 64-bit-offset 7700 Mixamo exports) and exposes:

* global settings: unit scale, up axis, frame rate;
* objects: models, bones, meshes, skins/clusters, animation stacks and curves;
* the bone parent chain from the ``Connections`` table;
* animation stack start/stop in FBX ``KTime`` units and the frame count;
* texture/video references (embedded PNG dimensions and unresolved absolute
  paths), used by the Android texture-memory review;
* a shared humanoid bone-convention table covering both the Mixamo
  (``mixamorig:LeftArm``) and Unreal-style (``upperarm_l``) naming used here.

It never writes to an FBX and never interprets a file as proof of what Unity
would import: it reports static evidence only. Actual Avatar validity, clip
playback and Android runtime behaviour still require the Unity Editor.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"

# FBX SDK KTime: one second is 46,186,158,000 units.
FBX_TIME_UNITS_PER_SECOND = 46_186_158_000

# KTime::ETimeMode values used by the files in this repository.
TIME_MODE_FPS = {
    1: 120.0, 2: 100.0, 3: 60.0, 4: 50.0, 5: 48.0,
    6: 30.0, 7: 30.0, 8: 29.97, 9: 29.97, 10: 25.0,
    11: 24.0, 12: 25.0, 13: 59.94, 14: 59.94, 15: 96.0,
    16: 72.0, 17: 59.94, 18: 119.88,
}

# Unity's Humanoid Avatar requires these 15 bones. Everything else (Chest,
# UpperChest, Neck, Shoulders, Toes, fingers, eyes...) is optional.
REQUIRED_HUMANOID_BONES = (
    "Hips", "Spine", "Head",
    "LeftUpperArm", "LeftLowerArm", "LeftHand",
    "RightUpperArm", "RightLowerArm", "RightHand",
    "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
    "RightUpperLeg", "RightLowerLeg", "RightFoot",
)

# Canonical humanoid bone -> accepted normalized bone names. Normalization is
# lowercase + removal of ':' '_' '-' ' ' + removal of the Mixamo namespace
# prefix, so both conventions share one table. This mirrors
# RigDetectionUtility.BoneAliases in the Unity Editor assembly.
HUMANOID_ALIASES: dict[str, tuple[str, ...]] = {
    "Hips": ("hips", "pelvis", "cog"),
    "Spine": ("spine", "spine01", "spine1"),
    "Chest": ("spine02", "spine2", "chest", "spine1"),
    "UpperChest": ("spine03", "spine3", "upperchest", "spine2"),
    "Neck": ("neck", "neck01"),
    "Head": ("head",),
    "LeftShoulder": ("leftshoulder", "claviclel"),
    "LeftUpperArm": ("leftupperarm", "leftarm", "upperarml"),
    "LeftLowerArm": ("leftlowerarm", "leftforearm", "lowerarml"),
    "LeftHand": ("lefthand", "handl"),
    "RightShoulder": ("rightshoulder", "clavicler"),
    "RightUpperArm": ("rightupperarm", "rightarm", "upperarmr"),
    "RightLowerArm": ("rightlowerarm", "rightforearm", "lowerarmr"),
    "RightHand": ("righthand", "handr"),
    "LeftUpperLeg": ("leftupperleg", "leftupleg", "thighl"),
    "LeftLowerLeg": ("leftlowerleg", "leftleg", "calfl"),
    "LeftFoot": ("leftfoot", "footl"),
    "LeftToes": ("lefttoes", "lefttoebase", "balll"),
    "RightUpperLeg": ("rightupperleg", "rightupleg", "thighr"),
    "RightLowerLeg": ("rightlowerleg", "rightleg", "calfr"),
    "RightFoot": ("rightfoot", "footr"),
    "RightToes": ("righttoes", "righttoebase", "ballr"),
}


class FbxError(ValueError):
    """Raised when a file is not a readable binary FBX 7.x document."""


@dataclass
class FbxNode:
    name: str
    properties: list[Any]
    children: list["FbxNode"]

    def child(self, name: str) -> "FbxNode | None":
        for item in self.children:
            if item.name == name:
                return item
        return None

    def children_named(self, name: str) -> list["FbxNode"]:
        return [item for item in self.children if item.name == name]

    def properties70(self) -> dict[str, list[Any]]:
        """Return the ``P`` entries of a ``Properties70`` block, by key."""
        result: dict[str, list[Any]] = {}
        block = self.child("Properties70")
        if block is None:
            return result
        for entry in block.children_named("P"):
            if not entry.properties:
                continue
            key = as_text(entry.properties[0])
            result.setdefault(key, []).append(
                entry.properties[-1] if entry.properties else None
            )
        return result


def as_text(value: Any) -> str:
    """FBX object names are ``b'name\\x00\\x01Type'``; keep the name only."""
    if isinstance(value, bytes):
        raw = value.split(b"\x00", 1)[0]
        return raw.decode("utf-8", "replace")
    return str(value)


def normalize_bone(name: str) -> str:
    """Lowercase, strip separators, and remove Mixamo's namespace prefix."""
    text = "".join(c.lower() for c in name if c not in ":_- ")
    if text.startswith("mixamorig"):
        tail = text[len("mixamorig"):]
        # Multi-model exports use "mixamorig1:LeftArm" etc.
        while tail and tail[0].isdigit():
            tail = tail[1:]
        text = tail
    return text


def _read_property(handle) -> Any:
    code = handle.read(1)
    if not code:
        raise FbxError("truncated property type")
    kind = chr(code[0])
    if kind == "Y":
        return struct.unpack("<h", handle.read(2))[0]
    if kind == "C":
        return handle.read(1)[0]
    if kind == "I":
        return struct.unpack("<i", handle.read(4))[0]
    if kind == "F":
        return struct.unpack("<f", handle.read(4))[0]
    if kind == "L":
        return struct.unpack("<q", handle.read(8))[0]
    if kind == "D":
        return struct.unpack("<d", handle.read(8))[0]
    if kind in "SR":
        length = struct.unpack("<I", handle.read(4))[0]
        return handle.read(length)
    if kind in "bcfdil":
        count, encoding, compressed = struct.unpack("<III", handle.read(12))
        raw = handle.read(compressed)
        if encoding == 1:
            raw = zlib.decompress(raw)
        if kind == "i":
            return struct.unpack("<%di" % count, raw)
        if kind == "l":
            return struct.unpack("<%dq" % count, raw)
        if kind == "f":
            return struct.unpack("<%df" % count, raw)
        if kind == "d":
            return struct.unpack("<%dd" % count, raw)
        if kind == "c":
            return tuple(raw[i] == 1 for i in range(count))
        return raw
    raise FbxError("unsupported FBX property type %r" % kind)


def _parse_nodes(data: bytes, version: int) -> list[FbxNode]:
    wide = version >= 7500
    end_size = 8 if wide else 4
    end_fmt = "<Q" if wide else "<I"
    header_fmt = "<QQQB" if wide else "<IIIB"
    header_size = struct.calcsize(header_fmt)
    limit = len(data)
    pos = 27  # 23-byte magic + 4-byte version

    def read_until(end: int) -> list[FbxNode]:
        nonlocal pos
        out: list[FbxNode] = []
        while pos < end:
            if pos + end_size > limit:
                raise FbxError("truncated node record header")
            end_offset = struct.unpack_from(end_fmt, data, pos)[0]
            if end_offset == 0:
                # Null terminator record: skip the remaining header bytes.
                pos += header_size
                break
            if end_offset > limit or end_offset <= pos:
                raise FbxError("invalid node end offset %d at %d" % (end_offset, pos))
            count, prop_length, name_length = struct.unpack_from(
                "<QQB" if wide else "<IIB", data, pos + end_size
            )
            cursor = pos + header_size
            name = data[cursor:cursor + name_length].decode("utf-8", "replace")
            cursor += name_length
            prop_start = cursor
            # Read properties from a cursor over the same buffer.
            props: list[Any] = []
            for _ in range(count):
                sub = _PropertyCursor(data, prop_start + prop_length, cursor)
                props.append(_read_property(sub))
                cursor = sub.pos
            cursor = prop_start + prop_length
            pos = cursor
            children = read_until(end_offset) if pos < end_offset else []
            pos = end_offset
            out.append(FbxNode(name, props, children))
        return out

    return read_until(limit)


class _PropertyCursor:
    """Minimal file-like view over ``data`` for ``_read_property``."""

    __slots__ = ("data", "limit", "pos")

    def __init__(self, data: bytes, limit: int, pos: int) -> None:
        self.data = data
        self.limit = limit
        self.pos = pos

    def read(self, size: int) -> bytes:
        if self.pos + size > self.limit:
            raise FbxError("property overruns its declared length")
        chunk = self.data[self.pos:self.pos + size]
        self.pos += size
        return chunk


def _read(data: bytes, size: int, pos: int) -> bytes:
    if pos + size > len(data):
        raise FbxError("truncated read at %d" % pos)
    return data[pos:pos + size]


@dataclass
class AnimationStackInfo:
    name: str
    start: int = 0
    stop: int = 0

    @property
    def seconds(self) -> float:
        return (self.stop - self.start) / FBX_TIME_UNITS_PER_SECOND


@dataclass
class TextureInfo:
    name: str
    filename: str
    width: int = 0
    height: int = 0
    embedded_bytes: int = 0
    external: bool = False


@dataclass
class FbxDocument:
    path: Path
    version: int
    size: int
    unit_scale: float
    up_axis: int
    frame_rate: float
    objects: list[FbxNode]
    connections: list[FbxNode]
    models: list[tuple[int, str, str]]
    bone_names: list[str]
    bone_parents: dict[str, str | None]
    mesh_names: list[str]
    cluster_count: int
    skin_count: int
    vertex_count: int
    geometry_count: int
    stacks: list[AnimationStackInfo]
    curve_count: int
    key_time_min: int | None
    key_time_max: int | None
    textures: list[TextureInfo]
    video_count: int

    @property
    def is_character(self) -> bool:
        return bool(self.mesh_names) and not self.stacks

    @property
    def is_animation(self) -> bool:
        return bool(self.stacks) and not self.mesh_names

    def humanoid_mapping(self) -> dict[str, str]:
        """Map canonical humanoid bone names to real bone names in this file."""
        by_normalized: dict[str, str] = {}
        for name in self.bone_names:
            key = normalize_bone(name)
            if key and key not in by_normalized:
                by_normalized[key] = name
        found: dict[str, str] = {}
        for canonical, aliases in HUMANOID_ALIASES.items():
            for alias in aliases:
                hit = by_normalized.get(alias)
                if hit:
                    found[canonical] = hit
                    break
        return found

    def missing_required_bones(self) -> list[str]:
        mapped = self.humanoid_mapping()
        return [b for b in REQUIRED_HUMANOID_BONES if b not in mapped]

    def core_hierarchy_connected(self) -> bool:
        """True when all 15 required bones descend from Hips in the skeleton."""
        mapped = self.humanoid_mapping()
        if "Hips" not in mapped:
            return False
        for canonical in REQUIRED_HUMANOID_BONES:
            if canonical not in mapped or canonical == "Hips":
                continue
            cursor = mapped[canonical]
            seen = set()
            while cursor and cursor not in seen:
                seen.add(cursor)
                if cursor == mapped["Hips"]:
                    break
                cursor = self.bone_parents.get(cursor)
            if cursor != mapped["Hips"]:
                return False
        return True

    def frames(self, stack: AnimationStackInfo | None = None) -> int:
        item = stack or (self.stacks[0] if self.stacks else None)
        if item is None:
            return 0
        return int(round(item.seconds * self.frame_rate))


def read_fbx(path: str | Path) -> FbxDocument:
    """Parse a binary FBX 7.x file and return its static description."""
    path = Path(path)
    data = path.read_bytes()
    if not data.startswith(MAGIC):
        raise FbxError("not a binary FBX 7.x file: %s" % path)
    if len(data) < 27:
        raise FbxError("truncated FBX header: %s" % path)
    version = struct.unpack_from("<I", data, 23)[0]
    if not 7000 <= version < 8000:
        raise FbxError("unsupported FBX version %d: %s" % (version, path))

    nodes = _parse_nodes(data, version)
    top = {node.name: node for node in nodes}
    global_settings = top.get("GlobalSettings")
    settings = global_settings.properties70() if global_settings else {}
    fps = TIME_MODE_FPS.get(int(settings.get("TimeMode", [0])[-1] or 0), 30.0)
    custom_fps = settings.get("CustomFrameRate", [None])[-1]
    if isinstance(custom_fps, (int, float)) and custom_fps > 0:
        fps = float(custom_fps)

    objects_node = top.get("Objects")
    objects = objects_node.children if objects_node else []
    connections_node = top.get("Connections")
    connections = connections_node.children if connections_node else []

    models: list[tuple[int, str, str]] = []
    id_to_model: dict[int, str] = {}
    bone_names: list[str] = []
    mesh_names: list[str] = []
    stacks: list[AnimationStackInfo] = []
    textures: list[TextureInfo] = []
    videos = 0
    clusters = 0
    skins = 0
    curves = 0
    vertex_count = 0
    geometry_count = 0
    key_times: list[int] = []

    for obj in objects:
        if obj.name == "Model" and len(obj.properties) >= 3:
            ident = int(obj.properties[0])
            name = as_text(obj.properties[1])
            kind = as_text(obj.properties[2])
            models.append((ident, name, kind))
            id_to_model[ident] = name
            if kind == "LimbNode":
                bone_names.append(name)
            elif kind == "Mesh":
                mesh_names.append(name)
        elif obj.name == "Deformer" and len(obj.properties) >= 3:
            kind = as_text(obj.properties[2])
            if kind == "Cluster":
                clusters += 1
            elif kind == "Skin":
                skins += 1
        elif obj.name == "Geometry":
            geometry_count += 1
            for child in obj.children:
                if child.name == "Vertices" and child.properties:
                    raw = child.properties[0]
                    if isinstance(raw, (bytes, tuple)):
                        vertex_count += len(raw) // 3
        elif obj.name == "AnimationCurve":
            curves += 1
            for child in obj.children:
                if child.name == "KeyTime" and child.properties:
                    values = child.properties[0]
                    if isinstance(values, tuple):
                        key_times.extend(int(v) for v in values)
        elif obj.name == "AnimationStack" and len(obj.properties) >= 2:
            props = obj.properties70()
            start = int(props.get("LocalStart", [0])[-1] or 0)
            stop = int(props.get("LocalStop", [0])[-1] or 0)
            if stop == 0 and key_times:
                stop = max(key_times)
            stacks.append(AnimationStackInfo(as_text(obj.properties[1]), start, stop))
        elif obj.name == "Texture" and len(obj.properties) >= 2:
            filename = ""
            for child in obj.children:
                if child.name in ("FileName", "RelativeFilename") and child.properties:
                    filename = as_text(child.properties[0])
                    break
            textures.append(TextureInfo(
                name=as_text(obj.properties[1]), filename=filename,
                external=filename.startswith(("C:", "D:", "/", "\\"))
                or ":\\" in filename,
            ))
        elif obj.name == "Video":
            videos += 1
            filename = ""
            content = b""
            for child in obj.children:
                if child.name in ("FileName", "RelativeFilename") and child.properties:
                    filename = as_text(child.properties[0])
                elif child.name == "Content" and child.properties:
                    raw = child.properties[0]
                    content = raw if isinstance(raw, bytes) else b""
            width = height = 0
            if content.startswith(b"\x89PNG") and len(content) >= 24:
                width, height = struct.unpack_from(">II", content, 16)
            for texture in textures:
                if texture.filename and texture.filename == filename and texture.width == 0:
                    texture.width = width
                    texture.height = height
                    texture.embedded_bytes = len(content)
                    break

    # Texture nodes and Video nodes are matched by filename; some FBXs store
    # the resolved path only on one side, so fall back to a positional pairing.
    video_info: list[tuple[str, int, int, int]] = []
    for obj in objects:
        if obj.name != "Video":
            continue
        filename = ""
        content = b""
        for child in obj.children:
            if child.name in ("FileName", "RelativeFilename") and child.properties:
                filename = as_text(child.properties[0])
            elif child.name == "Content" and child.properties:
                raw = child.properties[0]
                content = raw if isinstance(raw, bytes) else b""
        width = height = 0
        if content.startswith(b"\x89PNG") and len(content) >= 24:
            width, height = struct.unpack_from(">II", content, 16)
        video_info.append((filename, width, height, len(content)))
    for texture, (filename, width, height, size) in zip(textures, video_info):
        if not texture.embedded_bytes:
            texture.embedded_bytes = size
            texture.width = width
            texture.height = height
            if not texture.filename:
                texture.filename = filename
            texture.external = texture.external or (
                filename.startswith(("C:", "D:", "/", "\\")) or ":\\" in filename
            )

    # Connections: C("OO", child, parent) defines the transform hierarchy.
    # A model is also connected to its NodeAttribute/Geometry and to animation
    # layers, so only a parent that resolves to another Model is a skeleton
    # parent; the first such connection wins.
    parents: dict[int, int] = {}
    for connection in connections:
        props = connection.properties
        if len(props) < 3 or as_text(props[0]) != "OO":
            continue
        child, parent = int(props[1]), int(props[2])
        if child in parents or parent not in id_to_model:
            continue
        parents[child] = parent
    bone_parents: dict[str, str | None] = {}
    for name in bone_names:
        bone_parents[name] = None
    for ident, name, kind in models:
        if kind != "LimbNode":
            continue
        parent = parents.get(ident)
        parent_name = id_to_model.get(parent) if parent is not None else None
        bone_parents[name] = parent_name if parent_name in bone_parents else None

    return FbxDocument(
        path=path, version=version, size=len(data),
        unit_scale=float(settings.get("UnitScaleFactor", [1.0])[-1] or 1.0),
        up_axis=int(settings.get("UpAxis", [0])[-1] or 0),
        frame_rate=fps,
        objects=objects, connections=connections, models=models,
        bone_names=bone_names, bone_parents=bone_parents,
        mesh_names=mesh_names, cluster_count=clusters, skin_count=skins,
        vertex_count=vertex_count, geometry_count=geometry_count,
        stacks=stacks, curve_count=curves,
        key_time_min=min(key_times) if key_times else None,
        key_time_max=max(key_times) if key_times else None,
        textures=textures, video_count=videos,
    )
