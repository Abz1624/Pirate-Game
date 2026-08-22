"""Remove baked Island_C* demo objects from Run.unity."""
from __future__ import annotations

import re
from pathlib import Path

SCENE = Path(__file__).resolve().parents[1] / "Pirate Game Unity/Assets/_Game/Scenes/Run.unity"


def parse_blocks(text: str) -> list[tuple[int | None, str]]:
    parts = text.split("--- ")
    blocks: list[tuple[int | None, str]] = []
    for part in parts:
        match = re.search(r"&(\d+)\r?\n", part)
        block_id = int(match.group(1)) if match else None
        blocks.append((block_id, part))
    return blocks


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    blocks = parse_blocks(text)
    by_id = {block_id: part for block_id, part in blocks if block_id is not None}

    island_root_transform = next(
        block_id
        for block_id, part in blocks
        if block_id is not None
        and "Transform:" in part
        and re.search(r"m_GameObject: \{fileID: \d+\}", part)
        and "m_Name: IslandRoot" in by_id.get(
            int(re.search(r"m_GameObject: \{fileID: (\d+)\}", part).group(1)), ""
        )
    )

    transform_go: dict[int, int] = {}
    go_name: dict[int, str] = {}
    transform_children: dict[int, list[int]] = {}
    for block_id, part in blocks:
        if block_id is None:
            continue
        if "GameObject:" in part:
            name_match = re.search(r"m_Name: (.+)", part)
            if name_match:
                go_name[block_id] = name_match.group(1).strip()
        if "Transform:" not in part:
            continue
        go_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if go_match:
            transform_go[block_id] = int(go_match.group(1))
        if "m_Children:" in part:
            child_section = part.split("m_Children:", 1)[1].split("m_Father:", 1)[0]
            transform_children[block_id] = [
                int(value) for value in re.findall(r"\{fileID: (\d+)\}", child_section)
            ]
        else:
            transform_children[block_id] = []

    root_transforms = [
        transform_id
        for transform_id, game_object_id in transform_go.items()
        if go_name.get(game_object_id, "").startswith("Island_C")
    ]

    remove_ids: set[int] = set()
    stack = list(root_transforms)
    while stack:
        transform_id = stack.pop()
        if transform_id in remove_ids:
            continue
        remove_ids.add(transform_id)
        game_object_id = transform_go.get(transform_id)
        if game_object_id is not None:
            remove_ids.add(game_object_id)
            go_part = by_id.get(game_object_id, "")
            remove_ids.update(
                int(value) for value in re.findall(r"- component: \{fileID: (\d+)\}", go_part)
            )
        stack.extend(transform_children.get(transform_id, []))

    for block_id, part in blocks:
        if block_id is None or block_id in remove_ids:
            continue
        go_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if go_match and int(go_match.group(1)) in remove_ids:
            remove_ids.add(block_id)

    for block_id, part in blocks:
        if block_id is None or block_id in remove_ids:
            continue
        if "m_TransformParent: {fileID:" in part and any(
            f"{{fileID: {transform_id}}}" in part for transform_id in remove_ids
        ):
            remove_ids.add(block_id)

    kept: list[str] = []
    for block_id, part in blocks:
        if block_id is not None and block_id in remove_ids:
            continue
        if block_id == island_root_transform:
            part = re.sub(
                r"m_Children:\n(?:  - \{fileID: \d+\}\r?\n)+",
                "m_Children: []\n",
                part,
            )
            part = re.sub(
                r"m_Children:\n  - \{fileID: \d+\}\r?\n(?:  - \{fileID: \d+\}\r?\n)*",
                "m_Children: []\n",
                part,
            )
        kept.append(part)

    SCENE.write_text("--- ".join(kept), encoding="utf-8")
    print(f"Removed {len(remove_ids)} YAML blocks ({len(root_transforms)} Island_C roots)")


if __name__ == "__main__":
    main()
