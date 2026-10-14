# Victory objectives

VictoryObjectiveContracts.cs defines deterministic rules shared by scenario,
strategic AI, save/load and future UI adapters.

- Landmark requires an operational requiredBuildingCode.
- Sacred requires a control observation matching targetCode.
- Wonder requires an operational wonder and may require supporting buildings
  and technologies.

All objectives can require a minimum era, population and hold duration.
Evaluate is side-effect free. TryAdvance accepts only non-negative qualifying
control time and completes at the configured duration. Era and technology checks
use StrategicPlanningState, matching construction and research rules. The
contracts contain no UI, assets or scene references.
