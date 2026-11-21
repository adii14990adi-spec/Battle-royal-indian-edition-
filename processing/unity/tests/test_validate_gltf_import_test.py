import json
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT / "processing" / "unity"))
import validate_gltf_import_test as v  # noqa: E402


class SourceInventoryTests(unittest.TestCase):
    def test_live_source_manifest_has_23_exact_files(self):
        manifest = v.load_json(v.SOURCE_MANIFEST)
        self.assertEqual(23, manifest["file_count"])
        self.assertEqual(23, len(manifest["files"]))
        self.assertEqual("b3872f9276461dc3ecffed9e15731e98ed867d86", manifest["source_commit"])

    def test_original_and_test_copy_hashes(self):
        manifest = v.load_json(v.SOURCE_MANIFEST)
        for entry in manifest["files"]:
            rel = entry["path"]
            original = v.GLTF_ROOT / rel.replace("Male/", "Male/Original/", 1).replace("Female/", "Female/Original/", 1)
            self.assertTrue(original.is_file(), rel)
            self.assertEqual(entry["sha256"], v.sha256(original), rel)
            if not rel.endswith(".gltf"):
                sex, name = rel.split("/", 1)
                copy = v.GLTF_ROOT / sex / "UnityTestCopy" / Path(name).name
                self.assertTrue(copy.is_file(), str(copy))
                self.assertEqual(entry["sha256"], v.sha256(copy), str(copy))

    def test_original_uri_mismatches_are_documented_and_corrected(self):
        for sex, expected in v.KNOWN_URI_FIXES.items():
            original = v.GLTF_ROOT / sex / "Original" / f"Superhero_{sex}_FullBody.gltf"
            corrected = v.GLTF_ROOT / sex / "UnityTestCopy" / f"Superhero_{sex}_FullBody.gltf"
            original_uris = [u for _, _, u in v.gltf_uris(v.load_json(original))]
            corrected_uris = [u for _, _, u in v.gltf_uris(v.load_json(corrected))]
            self.assertTrue(set(expected).issubset(original_uris))
            self.assertFalse(set(expected).intersection(corrected_uris))
            for _, _, uri in v.gltf_uris(v.load_json(corrected)):
                self.assertTrue(v.uri_path(corrected.parent, uri).is_file(), uri)
            fixes = v.load_json(corrected.parent / "URI_FIXES.json")
            self.assertEqual(set(expected), {x["from"] for x in fixes["applied"]})


class GlTFAnalysisTests(unittest.TestCase):
    def test_gltf_structure_skin_and_counts(self):
        expected = {
            "Male": (8483, 14318),
            "Female": (8844, 15060),
        }
        for sex, (verts, tris) in expected.items():
            path = v.GLTF_ROOT / sex / "UnityTestCopy" / f"Superhero_{sex}_FullBody.gltf"
            result = v.analyse_gltf(path)
            self.assertEqual("2.0", result["asset"]["version"])
            self.assertEqual(69, result["nodes"])
            self.assertEqual(3, result["meshes"])
            self.assertEqual(3, result["materials"])
            self.assertEqual(1, result["skins"])
            self.assertEqual(65, result["joint_count"])
            self.assertEqual(65, result["unique_joint_names"])
            self.assertEqual(65, result["connected_joints"])
            self.assertEqual([], result["missing_required_bones"])
            self.assertEqual([], result["weight_problems"])
            self.assertEqual(verts, result["vertices"])
            self.assertEqual(tris, result["triangles"])
            self.assertEqual(7, len(result["pngs"]))
            self.assertTrue(all(x["exists"] for x in result["uri_results"]))

    def test_png_dimensions(self):
        male = v.analyse_gltf(v.GLTF_ROOT / "Male" / "UnityTestCopy" / "Superhero_Male_FullBody.gltf")
        dims = sorted((p["width"], p["height"]) for p in male["pngs"])
        self.assertIn((256, 256), dims)
        self.assertIn((2048, 2048), dims)


class RepositorySafetyTests(unittest.TestCase):
    def test_importer_is_pinned_and_modules_enabled(self):
        deps = v.load_json(v.PACKAGE_MANIFEST)["dependencies"]
        self.assertEqual("6.21.0", deps["com.unity.cloud.gltfast"])
        self.assertEqual("1.0.0", deps["com.unity.modules.unitywebrequest"])
        self.assertEqual("1.0.0", deps["com.unity.modules.unitywebrequesttexture"])
        self.assertEqual("1.0.0", deps["com.unity.modules.imageconversion"])

    def test_guid_uniqueness(self):
        owners, problems = v.guid_map(sorted(v.ASSETS_ROOT.rglob("*.meta")))
        self.assertEqual([], problems)
        # The 74 animation FBX .meta files were removed on 2026-11-20.
        self.assertGreaterEqual(len(owners), 38)

    def test_licence_findings(self):
        findings = v.licence_findings()
        self.assertTrue(findings["identical"])
        self.assertTrue(findings["cc0"])
        self.assertTrue(findings["creator"])
        self.assertTrue(findings["commercial"])
        self.assertFalse(findings["required_attribution"])

    def test_static_validator_passes_on_repository(self):
        report = v.run_validation()
        self.assertEqual([], [(r["check"], r["detail"]) for r in report.failures])
        self.assertGreater(report.not_verified_count, 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
