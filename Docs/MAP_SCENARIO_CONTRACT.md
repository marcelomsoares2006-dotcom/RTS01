# Map and scenario contract

`MapData` now carries deterministic player starts, strategic resource nodes and starting resource metadata. `MapScenarioRules.Validate` enforces the editor-free playable scenario contract:

- map identity, dimensions and 2–8 player bounds;
- exactly one in-bounds start per player;
- unique, in-bounds resource nodes with positive harvest data;
- at least one Wood, Gold and Stone node per player;
- explicit non-negative starting resources.

`EraProgressionCatalog` provides canonical Feudal, Castle and Imperial transition requirements so scenario setup, AI planning and future UI consume the same age-up definitions.

Unity still needs to bind these metadata entries to scene objects, terrain resource colliders and the runtime economy. The pure rules are covered by `MapScenarioRulesTests` in the existing EditMode assembly.
