# Strategic building eligibility

Territory influence, defense projections and victory building requirements use
`StrategicBuildingEligibilityRules`. A building contributes only when its
lifecycle is `Operational`, its owner is valid, and its content is explicitly
`RuntimeReady` with `contentRuntimeReady: true`.

Blueprints, under-construction, damaged, destroyed and ruined buildings are
excluded. Planned and model-intermediate catalog entries are excluded even if a
detached observation incorrectly reports an operational lifecycle. The gate is
pure and does not load prefabs or promote assets.
