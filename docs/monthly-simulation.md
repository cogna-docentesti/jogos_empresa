# Monthly simulation engine

The game settles a quarter of three monthly rounds. `MonthlySimulationEngine` calculates a statement without changing any input, accessing SQLite, or drawing random events. `RoundService` commits that statement and the updated session in one SQLite transaction. The in-memory session changes only after the transaction succeeds.

## Entry points

```csharp
// Read-only forecast from the shipped Resources catalogs.
var context = MonthlySimulationCatalog.Load(GameSessionState.Current);
var forecast = MonthlySimulationEngine.Calculate(context);

// Runtime UI integration: call from a Button OnClick event in the management hub.
// Add MonthlySettlementController to a scene object and select CloseMonth.
// The controller exposes LastResult and MonthSettled after a successful commit.

// Low-level settlement: explicit months make retries idempotent.
var result = new RoundService().ProcessRound(expectedMonth: 1);

// Ordered persisted statements.
var history = new RoundResultRepository(DatabaseInitializer.DatabaseService.Connection)
    .GetBySessionId(GameSessionState.Current.sessionId);
```

For a forecast including recorded events, pass `SessionEventHistoryRepository.GetBySessionAndRound(...)` as the second argument of `MonthlySimulationCatalog.Load`. `EstablishmentSummaryService.Build` already does this. For a completed session, the summary uses its last saved statement.

The summary refreshes the alignment score from that calculation and displays `Unavailable` with a diagnostic if the forecast cannot be calculated, instead of presenting a fabricated zero estimate.

In Play Mode, open **Game > Monthly Simulation** to preview the active month, settle it through the normal game flow, and inspect saved statements. Settlement changes the active player session. The window needs an initial configuration committed at D3 and a state of `Management_Hub` or `Round_Sales`.

## Rules and units

- A month has 30 operating days by default. Location demand and physical capacity are daily. Role support and equipment bonuses are monthly.
- Base demand is `LocationData.baseDailyDemand * operatingDays`. A positive `RestaurantData.estimatedMonthlyCustomers` caps that figure; zero uses the location's demand.
- The average ticket is the mean of saved product prices. Each customer buys one menu product; all products have equal sales shares. Saved prices must match the restaurant and remain within each product's range. The legacy `selectedPrice` field does not override the menu.
- Product supplies per customer are the mean of `selectedPrice * inputCostRatio`. Therefore supplies use the price-weighted mix of the existing product ratios, rather than a universal 30% assumption.
- Demand is base demand times recalculated alignment, price response, and opening reputation. Price response is `(referenceTicket / selectedTicket)^priceElasticity`, limited to 0.5-1.5 by default. Reference ticket includes `LocationData.referencePriceFactor`.
- Alignment uses the existing four-factor `AlignmentEngine`: HIGH 1.2, ADEQUATE 1.0, FRAGILE 0.75, CRITICAL 0.45. Required role quantities are now checked, not merely role presence.
- Opening reputation maps linearly from 0-100 to a demand multiplier of 0.5-1.5. This month's reputation changes influence next month's demand.
- Monthly capacity is the smaller of physical monthly capacity and team monthly support, with compatible equipment bonuses added to both limits. Missing required roles reduce capacity by the lowest covered fraction; missing required equipment closes the operation. An empty team cannot sell, even if equipment is owned.
- Daily customers are limited by daily capacity and stock effects. Daily totals are accumulated, then rounded down to whole monthly customers. Revenue and supplies are scaled to those whole customers while retaining the daily sales mix. Monetary amounts are rounded to cents and stored using the project's existing float columns.
- Payroll is the sum of role salary times quantity. Salaries already include employment charges in this educational model. Equipment purchase prices are not charged again during settlement.
- Rent comes from the selected location. Utilities come from the restaurant's `baseMonthlyCost`.
- Loans follow the existing declining-principal rule: remaining principal divided by remaining quarter months, plus interest on opening debt. The third month clears the remaining principal, including rounding residues. No new loan can be contracted in month three.
- `thirteenthSalary` is zero throughout the quarter. No annual payment or proportional accrual has been added.
- If at least 90% of demand is served, reputation gains 2 points plus a rounded quality bonus, capped at 5 extra points by default. Otherwise it loses 5 points. Event reputation changes are then included, with the final score limited to 0-100.
- The existing bankruptcy rule is retained: three consecutive negative `netResult` months produce BANKRUPT. Otherwise month three produces COMPLETED. A nonnegative month resets the loss counter. Negative cash alone does not immediately end a session.

`netResult = grossRevenue - TotalCosts + eventCashImpact` and `closingCash = openingCash + netResult`. `eventCashImpact` is signed: negative is money spent; positive is money received. `TotalCosts` includes supplies, rent, salaries, utilities, loan payment, and thirteenth salary; it excludes signed event cash. This is the game's cash-result model: loan principal is included as an outflow and is not separated into an accounting profit statement.

## Event settlement contract

`SessionEventHistoryRepository.RecordFromOption` saves an immutable JSON snapshot of the chosen effects. Recording queues the choice for monthly settlement; it does not update cash or reputation. Do not also call `AddCash` or `SetReputation` for the same choice, or those deltas will be applied twice. Do not put the same demand change into both `clientsChange` and `demandMultiplier`.

```csharp
var repository = new SessionEventHistoryRepository(db);
repository.RecordFromOption(session.sessionId, session.currentRound, day: 16,
    eventData, chosenOption);
```

`cashChange` and `reputationChange` settle once per recorded occurrence. `clientsChange` applies on the recorded day. IMMEDIATE and CURRENT_DAY operational multipliers apply on that day; CURRENT_MONTH applies from that day through the end of the month. Multiple active multipliers compose multiplicatively. Stock multiplies the available serving capacity. Effects never carry into another month. A legacy row without `effectsJson` settles its direct deltas only. Multiple events retain their full history; the single `RoundResultEntity.eventId` is populated only when exactly one event occurred, and `eventChoice` remains -1 because options have string IDs.

The event repository rejects new records for a settled month or a persisted session that is no longer in the requested active month.

This engine consumes recorded choices. It does not draw events, create an event decision screen, generate the final report, or add cloud synchronization.

## Catalog and balance configuration

`Assets/Resources/MonthlySimulation/DefaultSettings.asset` controls operating days, price elasticity and bounds, reputation demand bounds, service threshold, reputation gain/penalty, and quality scaling. Create assets through **Create > Game Data > Monthly Simulation Settings**, but the runtime loader expects the default asset at that exact Resources path.

New role assets live in `Resources/Roles`: attendant (2,200 / 900 monthly clients), grill_cook (2,500 / 750), sushi_chef (4,500 / 600), head_chef (8,000 / 450), manager (5,000 / 0). These are initial balance values, not an empirical calibration. Default required teams have two attendants and one restaurant specialist. Utilities are 1,500 for Lanches and Japones, and 2,000 for Frances. The existing location demand, capacity, rents, menu prices, and product input ratios are preserved. Equipment data loads from the existing **Equipments** folder.

## Persistence and failure handling

The existing `DatabaseInitializer.CreateTable` migration adds nullable columns without deleting prior rows. `RoundResultEntity` gains demand, capacity, average ticket, opening reputation, loan principal, and alignment diagnostics. `SessionEventHistoryEntity` gains `effectsJson`. Existing cash-result aliases remain available; `TotalCosts` is an ignored computed property.

Low-level settlement rejects missing catalogs, unknown selected IDs, invalid prices, invalid event days, nonfinite values, missing earlier month results, and stale persisted round/status. A new uncommitted setup is not settled. A SQLite failure rolls back both statement and session, leaving memory unchanged. Calling `ProcessRound(n)` again returns that month's saved row without applying it again; calling parameterless `ProcessRound()` intentionally settles the current active month. The service requires its own transaction.

`GameSessionService.ProcessCurrentRound` logs failures in English and stays in `Round_Sales`, allowing a retry. It advances UI states only after settlement succeeds. Historical rows created by the old placeholder engine are preserved and are not recalculated. Existing saves beyond month three continue to use the project's incompatible-save workflow.

## Validation

Run **Window > General > Test Runner > EditMode > Run All**. `MonthlySimulationTests` uses temporary ScriptableObjects and in-memory SQLite; it does not modify player saves. The suite covers a known monthly statement, catalog validation, prices, reputation, capacity, required team quantities, equipment, event scope and snapshots, signed cash, loans, the three-month cycle, database rollback, repeat requests, reload, forecast parity, and schema migration.

For headless execution, use the project's Unity 6000.3.20f1 editor with `-batchmode -nographics -runTests -testPlatform EditMode -testResults <path> -logFile <path>`. Use an isolated project copy when the project is already open in another Unity instance.

Validation completed on 2026-10-08 in an isolated Unity 6000.3.20f1 project copy: **58 Edit Mode tests passed, zero failed, exit code 0**. This includes 25 monthly simulation tests and 33 existing tests. The existing quarter-flow fixture now uses its own in-memory database rather than an outer rollback transaction, real menu/team catalog IDs, and a completed first month before testing loans started in month two. Desktop and mobile HTML checks found no document overflow or broken internal anchors.
