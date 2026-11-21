"""Check build 26660232 encounter accounting against an owned install (no game execution)."""

import argparse
import csv
import hashlib
import io
import json
import re
import subprocess
from collections import Counter, defaultdict
from pathlib import Path

from catalog import english_cards

ROOT = Path(__file__).resolve().parents[1]
BUILD = "26660232"
AUDIT = ROOT / "audit" / BUILD
ASSEMBLY_SHA = "a671e9efb86a7b80405133105665867b053c787670d475b4347cf5ccc15cb5fb"
FIELDNAMES = (
    "category",
    "id",
    "availability",
    "disposition",
    "evidence",
    "source_sha256",
    "fields",
)

# These labels describe static implementation coverage, not promises that an
# option is spawned or that all enabled alternatives have assessable effects.
EVENT_RULES = {}
for availability, disposition, labels in (
    ("enabled_only", "modeled", "resource sword"),
    (
        "enabled_only",
        "conditional_live",
        """
        upgrade card_fit resource_curse courier metamorphosis repeat_hp repeat_resource
        hp_relic hp_random_reward gold_random_reward gold_transform gold_random_upgrade
        gold_card_repeat hp_random_upgrade hp_enchant combat_hp resource_item
        curse_remove curse_enchant curse_relic curse_upgrade curse_random_reward
        curse_transform gold_modification gold_enchant hp_transform gold_remove gold_card
        remove_add
    """,
    ),
    (
        "enabled_only",
        "deferred",
        """
        combine transform combat random_card_pool relic_fit_gap minigame random_reward
        remove potion random_upgrade enchant barter duplicate_deck dynamic_removal
        deck_modification potion_trade craft trial key_trade delayed_reward
    """,
    ),
    ("enabled_only", "conditional_live", "gold_minigame party_transform"),
    ("transition", "conditional_live", "combat_transition advance"),
    ("transition", "not_ranked", "abandon_confirm"),
    ("terminal", "not_ranked", "exit terminal_text unavailable_exit"),
    ("locked", "not_ranked", "locked"),
    ("test_only", "not_ranked", "mock"),
):
    for label in labels.split():
        EVENT_RULES[label] = availability, disposition

# Audited generic paths. The allow-lists are checked against assembly enum fields
# or exact pack keys; no catch-all score is assigned to a new decision type.
ROOM = {
    "Unassigned": ("sentinel", "not_ranked", "room_enum_sentinel"),
    "Monster": ("enabled_only", "conditional_live", "battle_visible_intent"),
    "Elite": ("enabled_only", "conditional_live", "battle_visible_intent"),
    "Boss": ("enabled_only", "conditional_live", "battle_visible_intent_boss_catalog"),
    "Treasure": ("enabled_only", "conditional_live", "treasure_first_relic_inspection"),
    "Shop": ("enabled_only", "conditional_live", "shop_compare_cards_then_removal"),
    "Event": ("enabled_only", "conditional_live", "event_visible_effects"),
    "RestSite": ("enabled_only", "conditional_live", "camp_compare_heal_smith"),
    "Map": ("transition", "conditional_live", "route_travelable_only"),
}
MAP = {
    "Unassigned": ("sentinel", "not_ranked", "map_enum_sentinel"),
    "Unknown": ("random", "conditional_live", "route_unknown_room_not_predicted"),
    "Ancient": ("enabled_only", "conditional_live", "route_then_ancient_offer"),
    **{
        key: ("enabled_only", "conditional_live", "route_travelable_only")
        for key in ("Shop", "Treasure", "RestSite", "Monster", "Elite", "Boss")
    },
}
REWARD = {
    "None": ("sentinel", "not_ranked", "reward_enum_sentinel"),
    "Card": ("enabled_only", "conditional_live", "reward_rank_known_live_cards"),
    "Gold": ("enabled_only", "deferred", "reward_generic_only"),
    "Potion": ("enabled_only", "conditional_live", "reward_potion_slots_and_timing"),
    "Relic": ("enabled_only", "deferred", "reward_generic_only"),
    "RemoveCard": ("enabled_only", "deferred", "no_specialized_removal_reward"),
    "UpgradeCard": ("enabled_only", "conditional_live", "eligible_upgrade_overlay"),
    "SpecialCard": ("enabled_only", "deferred", "special_reward_result_not_modeled"),
}
MODES = {
    "None": ("sentinel", "not_ranked", "mode_enum_sentinel"),
    "Standard": ("mode_only", "conditional_live", "standard_runtime_paths"),
    "Daily": ("mode_only", "deferred", "daily_modifier_callbacks"),
    "Dread": ("mode_only", "deferred", "mode_not_verified"),
    "Blitz": ("mode_only", "deferred", "blitz_reward_picker_unmodeled"),
}
CAMP = {
    "OPTION_HEAL": ("enabled_only", "conditional_live", "camp_compare_heal_smith"),
    "OPTION_SMITH": ("enabled_only", "conditional_live", "camp_and_upgrade_overlay"),
    **{
        f"OPTION_{k}": ("run_dependent", "deferred", reason)
        for k, reason in {
            "CLONE": "camp_card_clone_unranked",
            "COOK": "camp_cook_unranked",
            "DIG": "camp_random_relic_unranked",
            "EVOLVE": "camp_evolve_unranked",
            "HATCH": "camp_pet_unranked",
            "KINDLE": "camp_kindling_unranked",
            "LIFT": "camp_lift_unranked",
            "MEND": "camp_coop_unverified",
            "RESTORE": "camp_restore_unranked",
        }.items()
    },
}
BLITZ = (
    "card_enchant_entry",
    "card_packs_entry",
    "card_removal_entry",
    "card_reward_of_each_rarity_entry",
    "card_rewards_entry",
    "card_transform_entry",
    "card_upgrade_entry",
    "colorless_card_reward_entry",
    "enchanted_card_reward_entry",
    "full_heal_and_max_hp_entry",
    "glam_entry",
    "potions_entry",
    "prismatic_card_reward_entry",
    "random_upgrades_entry",
    "rare_card_reward_entry",
    "relic_entry",
    "two_more_choices_entry",
    "two_relics_entry",
    "upgraded_card_reward_entry",
    "upgraded_rare_card_entry",
    "upgraded_rare_card_reward_entry",
)
CARD_ALTERNATES = {
    "OPTION_SKIP": ("enabled_only", "conditional_live", "card_reward_skip"),
    "OPTION_REROLL": ("run_dependent", "deferred", "future_roll_not_ranked"),
    "OPTION_SACRIFICE": ("run_dependent", "deferred", "alternate_reward_unranked"),
}
SHARED = {
    "events": {"GENERIC", "SHARED_EVENT_INFO"},
    "ancients": {"ERROR", "PROCEED"},
    "encounters": {"GENERIC", "NEOW", "NEOW_CHOICE"},
}
NO_EVENT_OPTIONS = {
    "DEPRECATED_EVENT": ("deprecated", "not_ranked", "retired_event_model"),
    "ERROR": ("locked", "not_ranked", "shared_locked_presentation"),
    "PROCEED": ("terminal", "not_ranked", "shared_proceed_presentation"),
    "FAKE_MERCHANT": (
        "run_dependent",
        "deferred",
        "fake_merchant_inventory_ui_and_combat",
    ),
}
CATALOG_ONLY_ENCOUNTERS = {"FLYCONID_WEAK", "OVERGROWTH_WILDLIFE"}
EVENT_RELICS = {
    "DOLL_ROOM": (
        "doll_take_dynamic_title",
        "BING_BONG DAUGHTER_OF_THE_WIND MR_STRUGGLES",
    ),
    "FAKE_MERCHANT": (
        "fake_shop_stock_and_foul_callback",
        """
        FAKE_ANCHOR FAKE_BLOOD_VIAL FAKE_HAPPY_FLOWER FAKE_LEES_WAFFLE FAKE_MANGO
        FAKE_MERCHANTS_RUG FAKE_ORICHALCUM FAKE_SNECKO_EYE FAKE_STRIKE_DUMMY FAKE_VENERABLE_TEA_SET
    """,
    ),
    "HUNGRY_FOR_MUSHROOMS": (
        "relic_description_fallback",
        "BIG_MUSHROOM FRAGRANT_MUSHROOM",
    ),
    "MUSEUM_OF_TORTURE": (
        "museum_display_roll",
        "OBSIDIAN_VICE THUMBSCREW TORMENTOUS_COLLAR",
    ),
    "TEA_MASTER": (
        "tea_effect_materialized_live",
        "BONE_TEA EMBER_TEA TEA_OF_DISCOURTESY",
    ),
    "THERMAE_NONUPEIPE": ("bath_effect_materialized_live", "BATH_ROBES BATH_TOWEL"),
}


def exact(label, expected, observed):
    expected, observed = set(expected), set(observed)
    if expected != observed:
        raise ValueError(
            f"{label}: missing {sorted(expected - observed)}; extra {sorted(observed - expected)}"
        )


def verify_release(pack, manifest, assembly):
    (profile,) = [
        p
        for p in json.loads((ROOT / "mod/catalogs/index.json").read_text())
        if p["build"] == BUILD
    ]
    release = json.loads((pack.parent / "release_info.json").read_text())
    if (release["version"], release["commit"], release["main_assembly_hash"]) != (
        profile["version"],
        profile["commit"],
        profile["assemblyHash"],
    ):
        raise ValueError(f"Pack release does not match Steam build {BUILD}")
    if manifest is not None:
        data = manifest.read_text()
        for key, value in (("appid", "2868840"), ("buildid", BUILD)):
            actual = re.findall(rf'(?m)^\s*"{key}"\s+"([^"]+)"\s*$', data)
            if actual != [value]:
                raise ValueError(f"Steam manifest {key} does not match {value}")
    if hashlib.sha256(assembly.read_bytes()).hexdigest() != ASSEMBLY_SHA:
        raise ValueError("Assembly does not match the audited macOS arm64 binary")


def read_event_policy():
    policy = {}
    for line in (AUDIT / "encounter-event-policy.tsv").read_text().splitlines():
        if not line or line.startswith("#"):
            continue
        root, options = line.split("\t")
        for spec in options.split():
            page, option = spec.split("/")
            option, tag = option.split("=")
            key = f"{root}.pages.{page}.options.{option}"
            if key in policy or tag not in EVENT_RULES:
                raise ValueError(
                    f"Duplicate ID or unrecognized event policy for {key}: {tag}"
                )
            policy[key] = tag
    return policy


def group(table, pattern, label):
    grouped = defaultdict(dict)
    for key, value in table.items():
        match = re.fullmatch(pattern, key)
        if match:
            group_id, field = match.groups()
            grouped[group_id][field] = value
        elif label == "option" and ".options." in key:
            raise ValueError(f"Unknown option key format: {key}")
    return grouped


def option_stems(table):
    return group(
        table, r"(\w+\.pages\.\w+\.options\.\w+)\.(title|description)", "option"
    )


def scoped_keys(table, root):
    return {k: v for k, v in table.items() if k == root or k.startswith(root + ".")}


def slug(type_name):
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", type_name).upper()


def assembly_inventory(assembly):
    result = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(ROOT / "tools/encounter_audit"),
            "--",
            str(assembly),
        ],
        check=True,
        stdout=subprocess.PIPE,
        text=True,
    )
    values = defaultdict(list)
    for line in result.stdout.splitlines():
        category, *cells = line.split("\t")
        if category not in {
            "ancient_relic",
            "event_relic",
            "event_model",
            "encounter_model",
            "map_point",
            "mode",
            "neow_modifier",
            "reward",
            "room",
        } or len(cells) != (3 if category.endswith("_relic") else 1):
            raise ValueError(
                f"Unexpected metadata extractor row category or shape: {category}"
            )
        values[category].append(cells)
    return values


def audit(pack, assembly):
    tables = {
        name: english_cards(pack, name)
        for name in (
            "events",
            "ancients",
            "encounters",
            "relics",
            "rest_site_ui",
            "merchant_room",
            "card_reward_ui",
            "ascension",
            "modifiers",
        )
    }
    source = assembly_inventory(assembly)
    inventory = defaultdict(set)
    with (AUDIT / "inventory.tsv").open() as stream:
        for row in csv.DictReader(stream, delimiter="\t"):
            kind, identifier = row["category"], row["beta_id"]
            if not identifier:
                continue
            if identifier in inventory[kind]:
                raise ValueError(f"Duplicate inventory ID: {kind}/{identifier}")
            inventory[kind].add(identifier)

    output = {}

    def row(
        kind,
        key,
        availability,
        disposition,
        evidence,
        fields=None,
        *,
        show_fields=False,
    ):
        payload = (
            hashlib.sha256(
                json.dumps(
                    fields,
                    sort_keys=True,
                    separators=(",", ":"),
                    ensure_ascii=True,
                ).encode()
            ).hexdigest()
            if fields is not None
            else ASSEMBLY_SHA
        )
        field_names = ",".join(sorted(fields)) if show_fields and fields else "-"
        if (kind, key) in output:
            raise ValueError(f"Duplicate audit row: {kind}/{key}")
        output[kind, key] = (availability, disposition, evidence, payload, field_names)

    opts = option_stems(tables["events"])
    policy = read_event_policy()
    exact("Event option policy vs shipped keys", opts, policy)
    for key, fields in opts.items():
        tag = policy[key]
        row("event_option", key, *EVENT_RULES[tag], tag, fields, show_fields=True)

    ancient_opts = option_stems(tables["ancients"])
    exact(
        "Ancient localized page options",
        {"OROBAS.pages.INITIAL.options.OPTION_POOL_3_LOCKED"},
        ancient_opts,
    )
    for key, fields in ancient_opts.items():
        row(
            "ancient_option",
            key,
            "locked",
            "not_ranked",
            "orobas_pool_3_no_eligible_relic",
            fields,
            show_fields=True,
        )

    for kind, field in (
        ("events", "title"),
        ("ancients", "epithet"),
        ("encounters", "title"),
    ):
        catalog = tables[kind]
        known = {k.split(".")[0] for k in catalog if k.split(".")[1:] == [field]}
        exact(f"Owned {kind} IDs vs inventory", known, inventory[kind])
        exact(
            f"{kind} catalog roots",
            known | SHARED[kind],
            {k.split(".")[0] for k in catalog},
        )
        for key in SHARED[kind]:
            claim = NO_EVENT_OPTIONS.get(
                key, ("metadata", "not_ranked", "shared_catalog_not_model")
            )
            row("shared_catalog", f"{kind}.{key}", *claim, scoped_keys(catalog, key))

    roots_with_options = {k.split(".")[0] for k in opts}
    exact(
        "Event roots without local options",
        NO_EVENT_OPTIONS,
        inventory["events"] - roots_with_options,
    )
    event_models = {slug(name) for (name,) in source["event_model"]}
    exact(
        "Concrete EventModel classes",
        (inventory["events"] - {"ERROR", "PROCEED"})
        | inventory["ancients"]
        | {"DEPRECATED_ANCIENT_EVENT"},
        event_models,
    )
    for root in inventory["events"]:
        if root in NO_EVENT_OPTIONS:
            availability, disposition, evidence = NO_EVENT_OPTIONS[root]
        elif root == "MOCK_EVENT_MODEL":
            availability, disposition, evidence = (
                "test_only",
                "not_ranked",
                "mock_event_model",
            )
        else:
            availability, disposition, evidence = (
                "enabled_only",
                "conditional_live",
                "per_option_policy",
            )
        row(
            "event",
            root,
            availability,
            disposition,
            evidence,
            scoped_keys(tables["events"], root),
        )
    row(
        "ancient_model",
        "DEPRECATED_ANCIENT_EVENT",
        "deprecated",
        "not_ranked",
        "retired_event_model",
    )

    for root in inventory["ancients"]:
        disposition, evidence = (
            ("not_ranked", "architect_dialogue_only")
            if root == "THE_ARCHITECT"
            else (
                "conditional_live",
                "ancient_pool_visible_only",
            )
        )
        row(
            "ancient",
            root,
            "transition" if root == "THE_ARCHITECT" else "run_dependent",
            disposition,
            evidence,
            scoped_keys(tables["ancients"], root),
        )

    known_models = {slug(name) for (name,) in source["encounter_model"]}
    exact(
        "Catalog vs concrete combat models",
        CATALOG_ONLY_ENCOUNTERS,
        inventory["encounters"] - known_models,
    )
    mocks = {
        "MOCK_ARTIFACT_ENCOUNTER",
        "MOCK_ATTACK_AND_SUMMON_ENCOUNTER",
        "MOCK_BOSS_ENCOUNTER",
        "MOCK_ELITE_ENCOUNTER",
        "MOCK_MONSTER_ENCOUNTER",
        "MOCK_NO_REWARDS_ENCOUNTER",
        "MOCK_PLATING_ENCOUNTER",
        "MOCK_TWO_MONSTER_ENCOUNTER",
    }
    exact("Assembly-only combat models", mocks, known_models - inventory["encounters"])
    for key in mocks:
        row("encounter_model", key, "test_only", "not_ranked", "mock_combat_model")
    bosses = {
        line.split("\t", 1)[0]
        for line in (ROOT / "mod/catalogs" / BUILD / "bosses.tsv")
        .read_text()
        .splitlines()
    }
    exact(
        "Boss tactic catalog IDs",
        {k for k in inventory["encounters"] if k.endswith("_BOSS")},
        bosses,
    )
    for key in inventory["encounters"]:
        if key in CATALOG_ONLY_ENCOUNTERS:
            status = "catalog_only", "deferred", "no_matching_concrete_combat_model"
        elif key == "DEPRECATED_ENCOUNTER":
            status = "deprecated", "not_ranked", "retired_combat_model"
        elif key in bosses:
            status = (
                "enabled_only",
                "conditional_live",
                "battle_visible_intent_boss_catalog",
            )
        elif key.endswith("_ENCOUNTER"):
            status = (
                "run_dependent",
                "conditional_live",
                "event_combat_then_visible_intent",
            )
        else:
            status = "enabled_only", "conditional_live", "battle_visible_intent"
        row("encounter", key, *status, scoped_keys(tables["encounters"], key))

    for category, claims in (
        ("room", ROOM),
        ("map_point", MAP),
        ("reward", REWARD),
        ("mode", MODES),
    ):
        exact(category + " enum", claims, {v for (v,) in source[category]})
        for key, claim in claims.items():
            row(category, key, *claim)

    camp = group(tables["rest_site_ui"], r"(OPTION_\w+)\.(\w+)", "camp")
    exact("Rest-site actions", CAMP, camp)
    for key, fields in camp.items():
        row("rest_action", key, *CAMP[key], fields, show_fields=True)
    merchant = group(
        tables["merchant_room"],
        r"(MERCHANT\.blitz\.\w+|MERCHANT\.cardRemovalService)\.(title|description)",
        "merchant",
    )
    exact(
        "Merchant localized actions",
        {"MERCHANT.cardRemovalService", *(f"MERCHANT.blitz.{k}" for k in BLITZ)},
        merchant,
    )
    for key, fields in merchant.items():
        claim = (
            ("mode_only", "deferred", "blitz_merchant_perk_not_ranked")
            if ".blitz." in key
            else (
                "enabled_only",
                "conditional_live",
                "removal_if_stocked_and_affordable",
            )
        )
        row("merchant_action", key, *claim, fields, show_fields=True)
    alternates = group(tables["card_reward_ui"], r"(OPTION_\w+)\.(\w+)", "card_reward")
    exact("Card reward alternates", CARD_ALTERNATES, alternates)
    for key, fields in alternates.items():
        row("card_reward_action", key, *CARD_ALTERNATES[key], fields, show_fields=True)

    relics = tables["relics"]
    # Match concrete type identities to installed canonical relic IDs, not translated titles.
    relic_ids = {
        k.split(".")[0] for k in relics if k.endswith(".title") and k.count(".") == 1
    }
    exact("Relic IDs vs inventory", inventory["relics"], relic_ids)
    canonical = {identifier.replace("_", ""): identifier for identifier in relic_ids}
    if len(canonical) != len(relic_ids):
        raise ValueError("Ambiguous assembly relic-name normalization")
    for kind in ("ancient_relic", "event_relic"):
        pools = defaultdict(set)
        for owner, name, method in source[kind]:
            pools[owner, canonical[name.upper()]].add(method)
        if kind == "ancient_relic":
            exact(
                "Ancient relic pool owners",
                inventory["ancients"] - {"THE_ARCHITECT"},
                {owner for owner, _ in pools},
            )
            declared = set()
            for line in (
                (ROOT / "mod/catalogs" / BUILD / "ancient-gifts.tsv")
                .read_text()
                .splitlines()
            ):
                owner, relic, fit, cue = line.split("\t")
                if (owner, relic) in declared or not cue.strip() or not fit.strip():
                    raise ValueError(
                        f"Duplicate or incomplete gift rule: {owner}/{relic}"
                    )
                declared.add((owner, relic))
            exact("Runtime Ancient gift rules vs assembly pools", pools, declared)
        else:
            exact(
                "Classified event relic payloads vs assembly",
                pools,
                {
                    (owner, relic)
                    for owner, (_, ids) in EVENT_RELICS.items()
                    for relic in ids.split()
                },
            )
        for (owner, relic), methods in pools.items():
            if kind == "event_relic":
                tag = EVENT_RELICS[owner][0]
                disposition = "deferred"
            else:
                disposition = "conditional_live"
                tag = "build_specific_gift_tradeoff"
            key = (
                f"{owner}.pages.INITIAL.options.{relic}"
                if kind == "ancient_relic"
                else f"{owner}.{relic}"
            )
            row(
                kind,
                key,
                "run_dependent",
                disposition,
                tag + ":" + ",".join(sorted(methods)),
            )

    for key, value in relics.items():
        if key.endswith(".eventDescription"):
            row(
                "relic_text_source",
                key,
                "metadata",
                "not_ranked",
                "potential_relic_description_fallback",
                {key: value},
            )
    exact(
        "Neow modifier actions",
        {"ALL_STAR", "DRAFT", "INSANITY", "SEALED_DECK", "SPECIALIZED"},
        {slug(name) for (name,) in source["neow_modifier"]},
    )
    for (name,) in source["neow_modifier"]:
        key = slug(name)
        fields = scoped_keys(tables["modifiers"], key)
        if not fields:
            raise ValueError(f"Missing localized modifier ID: {key}")
        row(
            "neow_modifier",
            f"NEOW.{key}",
            "mode_only",
            "conditional_live",
            "neow_sequential_opening",
            fields,
        )
    for key in ("CONTINUE", "RESPOND"):
        identifier = f"THE_ARCHITECT.{key}"
        row(
            "event_control",
            identifier,
            "transition",
            "not_ranked",
            "architect_dialogue_control",
            {identifier: tables["ancients"][identifier]},
        )
    row(
        "event_control",
        "THE_ARCHITECT.PROCEED",
        "terminal",
        "not_ranked",
        "architect_proceed",
    )
    row(
        "event_control",
        "NEOW.BLITZ",
        "mode_only",
        "conditional_live",
        "blitz_neow_opening",
        scoped_keys(tables["ascension"], "BLITZ.NEOW"),
    )

    for key, claim in DYNAMIC.items():
        row("runtime_family", key, *claim)
    return output


DYNAMIC = {
    "ANCIENT.RELIC_VARIANT": (
        "run_dependent",
        "deferred",
        "materialized_player_and_rng_state",
    ),
    "ANCIENT.DEBUG_OVERRIDE": (
        "test_only",
        "not_ranked",
        "ancient_generate_wrapper_debug_override",
    ),
    "EVENT.GENERIC_PROCEED": (
        "terminal",
        "not_ranked",
        "event_option_is_proceed_excluded",
    ),
    "EVENT.CHOSEN_OR_DISABLED": (
        "locked",
        "not_ranked",
        "event_requires_current_enabled_unchosen_buttons",
    ),
    "EVENT.GENERIC_ERROR": ("locked", "not_ranked", "event_option_is_locked_excluded"),
    "EVENT.COOP_SETTLEMENT": (
        "run_dependent",
        "deferred",
        "party_effect_resolution_not_verified",
    ),
    "CRYSTAL_SPHERE.DIVINATION_BOARD": (
        "run_dependent",
        "deferred",
        "minigame_clicks_and_hidden_results",
    ),
    "DOLL_ROOM.RELIC_PICK": ("run_dependent", "deferred", "doll_take_dynamic_title"),
    "FAKE_MERCHANT.INVENTORY": ("run_dependent", "deferred", "fake_shop_custom_layout"),
    "FAKE_MERCHANT.FOUL_POTION": (
        "run_dependent",
        "deferred",
        "fake_shop_foul_potion_callback",
    ),
    "NEOW.MODIFIER_QUEUE": (
        "mode_only",
        "conditional_live",
        "modifier_options_depend_on_run",
    ),
    "RANWID_THE_ELDER.OFFERED_ITEM": (
        "run_dependent",
        "deferred",
        "chosen_relic_or_potion_id_and_trade",
    ),
    "RELIC_TRADER.OFFERED_SWAP": (
        "run_dependent",
        "deferred",
        "rolled_owned_to_new_relic_pair",
    ),
    "SLIPPERY_BRIDGE.REMOVAL_CARD": (
        "run_dependent",
        "deferred",
        "rolled_target_and_reused_option_key",
    ),
    "THE_ARCHITECT.DIALOGUE_INDEX": (
        "transition",
        "not_ranked",
        "indexed_dialogue_advance_control",
    ),
    "THE_FUTURE_OF_POTIONS.TRADE": (
        "run_dependent",
        "deferred",
        "rolled_card_reward_and_visible_potion",
    ),
    "THERMAE_NONUPEIPE.DRINK_RELIC": (
        "run_dependent",
        "deferred",
        "nonupeipe_pool_filtered_and_sampled",
    ),
    "TINKER_TIME.CARD_BUILDER": (
        "run_dependent",
        "deferred",
        "offered_card_type_rider_and_cost",
    ),
    "SHOP.CARD": (
        "run_dependent",
        "conditional_live",
        "shop_rank_stocked_affordable_card",
    ),
    "SHOP.RELIC": ("run_dependent", "deferred", "shop_does_not_rank_relics"),
    "SHOP.POTION": ("run_dependent", "deferred", "shop_does_not_rank_potions"),
    "SHOP.FOUL_POTION": ("run_dependent", "deferred", "throw_target_effect_not_ranked"),
    "SHOP.CARD_REMOVAL_TARGET": (
        "run_dependent",
        "deferred",
        "shop_removal_eligibility_not_target_comparison",
    ),
    "SHOP.SKIP_OR_LEAVE": ("terminal", "not_ranked", "budget_fallback_not_global_plan"),
    "TREASURE.RELIC": (
        "run_dependent",
        "conditional_live",
        "treasure_first_relic_inspection",
    ),
    "TREASURE.PROCEED": ("terminal", "not_ranked", "treasure_completion_control"),
    "SELECTION.BUNDLE": ("run_dependent", "deferred", "bundle_generic_context_only"),
    "SELECTION.CARD": (
        "run_dependent",
        "conditional_live",
        "known_visible_offers_only",
    ),
    "SELECTION.CARD_HAND": (
        "run_dependent",
        "conditional_live",
        "combat_hand_prompt_generic",
    ),
    "SELECTION.CARD_REMOVE": (
        "run_dependent",
        "deferred",
        "no_generic_removal_target_ranking",
    ),
    "SELECTION.CARD_TRANSFORM": (
        "run_dependent",
        "conditional_live",
        "transform_basic_heuristic",
    ),
    "SELECTION.CARD_UPGRADE": (
        "run_dependent",
        "conditional_live",
        "remaining_legal_upgrade_targets",
    ),
    "SELECTION.CARD_ENCHANT": (
        "run_dependent",
        "deferred",
        "no_generic_enchantment_target_ranking",
    ),
    "SELECTION.RELIC": (
        "run_dependent",
        "conditional_live",
        "known_positive_relic_fit_only",
    ),
    "SELECTION.GENERIC": (
        "run_dependent",
        "deferred",
        "generic_prompt_not_option_scoring",
    ),
    "COMBAT.PLAY_CARD": (
        "run_dependent",
        "conditional_live",
        "battle_legal_affordable_known_actions",
    ),
    "COMBAT.POTION": ("run_dependent", "conditional_live", "battle_potion_timing"),
    "COMBAT.END_TURN": (
        "run_dependent",
        "conditional_live",
        "battle_no_safe_play_heuristic",
    ),
    "COMBAT.HIDDEN_INTENT": (
        "locked",
        "not_ranked",
        "battle_hidden_intent_suppression",
    ),
}


def render(rows):
    buf = io.StringIO()
    writer = csv.writer(buf, delimiter="\t", lineterminator="\n")
    writer.writerow(FIELDNAMES)
    writer.writerows((*key, *data) for key, data in sorted(rows.items()))
    return buf.getvalue()


def check_snapshot(path, rows):
    with path.open() as stream:
        actual = stream.read()
    reader = csv.DictReader(io.StringIO(actual), delimiter="\t")
    if reader.fieldnames != list(FIELDNAMES):
        raise ValueError("Coverage snapshot header differs")
    existing = {}
    for item in reader:
        key = item["category"], item["id"]
        if key in existing:
            raise ValueError(f"Duplicate coverage snapshot ID: {key}")
        existing[key] = tuple(item[k] for k in FIELDNAMES[2:])
    exact("Coverage snapshot vs audit sources", rows, existing)
    changed = [key for key in sorted(rows) if rows[key] != existing[key]]
    if changed:
        raise ValueError(f"Coverage row drift: {changed}")
    if actual != render(rows):
        raise ValueError("Coverage snapshot formatting differs")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pack", type=Path)
    parser.add_argument(
        "--assembly", type=Path, help="Exact arm64 sts2.dll (default: beside the PCK)"
    )
    parser.add_argument(
        "--steam-manifest",
        type=Path,
        help="appmanifest_2868840.acf for an archived/nonstandard layout",
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--check", action="store_true", help="Check the committed snapshot (default)"
    )
    mode.add_argument(
        "--write",
        action="store_true",
        help="Regenerate the audit TSV from the explicit policy",
    )
    args = parser.parse_args()
    pack = args.pack.resolve()
    assembly = args.assembly or pack.parent / "data_sts2_macos_arm64/sts2.dll"
    manifest = args.steam_manifest or next(
        (p / "appmanifest_2868840.acf" for p in pack.parents if p.name == "steamapps"),
        None,
    )
    try:
        verify_release(pack, manifest, assembly)
        rows = audit(pack, assembly.resolve())
        path = AUDIT / "encounter-coverage.tsv"
        if args.write:
            path.write_text(render(rows))
        else:
            check_snapshot(path, rows)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Encounter coverage failed: {error}\n")
    print(f"Build {BUILD}: {len(rows)} accounted rows; static inventory only")
    print("Categories:", dict(sorted(Counter(k[0] for k in rows).items())))
    print(
        "Event options:",
        dict(
            sorted(
                Counter(v[1] for k, v in rows.items() if k[0] == "event_option").items()
            )
        ),
    )
    print("Live reachability and simulation completeness are not checked.")


if __name__ == "__main__":
    main()
