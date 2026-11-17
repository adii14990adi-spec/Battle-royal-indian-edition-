#!/usr/bin/env python3
"""Emit GitHub error annotations for failed Unity Test Runner cases.

Usage: print_test_failures.py <results.xml> [EditMode|PlayMode]

The inline python one-liner this replaces could not survive the YAML/bash
quoting layers (the escaped quotes reached Python literally), so failures
were silently swallowed by ``|| true``. This file parses the NUnit XML
directly instead.

* every ``test-case`` with result="Failed" becomes one ::error annotation
  with the failure message (newline-normalised, length-capped),
* when no test-case failed but the run itself failed (setup/compile
  problems), the suite-level result is annotated so the step never exits
  without saying why.
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: print_test_failures.py <results.xml> [platform]", file=sys.stderr)
        return 2

    platform = argv[2] if len(argv) > 2 else "Tests"
    try:
        tree = ET.parse(argv[1])
    except (OSError, ET.ParseError) as error:
        print(f"::error title={platform} results::{error}")
        return 1

    failed = 0
    for case in tree.iter("test-case"):
        if case.attrib.get("result") != "Failed":
            continue
        failed += 1
        name = case.attrib.get("name", "?")
        message = case.findtext("failure/message") or case.findtext("reason/message") or ""
        message = message.replace("\r", " ").replace("\n", " | ")[:800]
        print(f"::error title={platform} {name}::{message}")

    if failed == 0:
        run = tree.getroot()
        print(
            "::error title={0} suite::result={1} total={2} passed={3} failed={4} inconclusive={5}".format(
                platform,
                run.attrib.get("result", "?"),
                run.attrib.get("total", "?"),
                run.attrib.get("passed", "?"),
                run.attrib.get("failed", "?"),
                run.attrib.get("inconclusive", "?"),
            )
        )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
