# Monthly simulation engine

The game settles a quarter of three monthly rounds. `MonthlySimulationEngine` calculates a statement without changing any input, accessing SQLite, or drawing random events. `RoundService` commits that statement and the updated session in one SQLite transaction. The in-memory session changes only after the transaction succeeds.

## Quarterly comparison (UI-14)

`QuarterlyResultController` queries `RoundResultRepository.GetBySessionId` for the current campaign on every visit. The hub opens `PanelId.QuarterlyResults = 12`; the monthly result's **Comparar trimestre** action also opens the comparison while `Round_Summary` waits, including after month three. **Voltar** restores that monthly summary and its focus, or the hub's navigation history. Reading, retrying, scrolling and returning never settle or save a month.

`QuarterlyResultPresentation` keeps three explicit month slots. Missing months show **Não salvo** in both the table and chart; partial totals include only saved records. Duplicated months, records from another campaign, months outside 1–3 and nonfinite saved amounts produce an error state. No saved rows produces the empty state.

The aggregate column states its rule on every row:

- Revenue, outflows, signed events, cash results, loan details, customers and demand sum the saved months. Monthly capacity sums the available service slots across those months.
- Opening cash uses the first saved month's opening; closing cash uses the last saved month's closing. Reputations use those same opening/closing endpoints. Gaps remain visible and are never filled with invented balances.
- Ticket uses the saved monthly ticket weighted by saved customers. With no customers it shows **Sem clientes**. Alignment score and coherence use simple monthly means; classification uses the last saved label, translated only for display. Reputation deltas sum the saved monthly changes.
- Variation is the last available month's value minus the first available month's value, in the metric's own units, with explicit month labels. It requires two saved months; classification has no numerical variation. No percentage growth is implied when the baseline is zero or negative.

Monetary aggregates sum the displayed cent values of the persisted floats, using `double` only in this read-only presentation. This avoids adding binary approximation residues above R$ 100,000. Nothing is written back. Loan principal and interest remain details already included once in `loanPayment`; signed event cash is added once by the existing settlement, never reapplied by the report. The quarter chart shows each saved `netResult` on a shared signed scale.

## Entry points

```csharp
// Read-only forecast from the shipped Resources catalogs.
var context = MonthlySimulationCatalog.Load(GameSessionState.Current);
var forecast = MonthlySimulationEngine.Calculate(context);

// Runtime UI integration: call from a Button OnClick event in the management hub.
// Add MonthlySettlementController to a scene object and select CloseMonth.
// The controller exposes LastResult and MonthSettled after a successful commit.

// GameScene UI-12: CloseMonth first opens EventScreenController.OpenMonth.
// It reserves the existing selector's draw, waits for every choice, then settles.

// Low-level settlement: explicit months make retries idempotent.
var result = new RoundService().ProcessRound(expectedMonth: 1);

// Ordered persisted statements.
var history = new RoundResultRepository(DatabaseInitializer.DatabaseService.Connection)
    .GetBySessionId(GameSessionState.Current.sessionId);

// UI-13: acknowledge the saved statement only after the player reads it.
service.ContinueAfterSummary(savedResult.roundResultId);
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

### Event UI lifecycle (UI-12)

The GameScene hub has **Fechar mês** and **Eventos e histórico** actions. `MonthlySettlementController.CloseMonth()` starts the month and holds `Round_Sales` while `EventScreenController.OpenMonth(onCompleted)` presents the selected events. It does not settle before the choices. A pending decision rejects `Close()` and additional settlement requests. Only the last confirmation releases the callback and lets the existing `ProcessCurrentRound()` settlement run. The later, brief `Round_Event` transition is retained for compatibility and has a panel reference, but is no longer the player's only interaction opportunity. The remaining round/result orchestration is outside UI-12.

`MonthlyEventDecisionService.OpenMonth` passes the real restaurant, equipment, session history, monthly counts and exclusion groups to `MonthlyEventSelector`. Its default four-event draw is stored once in `MonthlyEventPlanEntity`, keyed by session/month, so reopening or reloading cannot reroll it. New event days are spread evenly through the configured operating days: four events in a 30-day month occur on days 6, 12, 18 and 24. Fewer eligible events remain fewer events; the UI does not force invalid candidates. Restart removes these reservations together with the existing session/history in the same transaction.

`OpenDecision(EventData, month, day, onCompleted)` is the explicit presentation entry point for a future orchestrator. `IsAwaitingChoice`, `ChoiceConfirmed`, `Closed` and `Close()` expose the waiting/confirmation lifecycle. Every catalog option is presented, with native text for its real effects and duration. The twenty illustrations are serialized by existing event ID in `EventScreenView`; they do not alter the economic catalog. History shows persisted choice/effect snapshots, including an empty state, and can be consulted without releasing a pending decision.

Confirmation is transactional and idempotent by session/month/event/day. Retrying the same choice returns its existing row, including after settlement; trying a different choice for the same occurrence fails. It calls `SessionEventHistoryRepository.RecordFromOption` only for a new occurrence. It never calls `AddCash` or `SetReputation`. The sixty shipped options are verified against forecast/settlement parity and repeat settlement in isolated SQLite tests.

## Monthly result UI lifecycle (UI-13)

`ProcessCurrentRound()` commits through `RoundService` and enters `Round_Summary`, where it now waits. `MonthlyResultController` reads the latest saved statement for the current session, including completed/bankrupt sessions. Its `Continue()` action calls `GameSessionService.ContinueAfterSummary(resultId)`. That method checks the summary state and saved ID, then follows the existing hub, final-report or bankruptcy destination without recalculation or economic writes. Finished campaigns persist only the presentation acknowledgement in `finalSummaryAcknowledged`. Repeated acknowledgement cannot settle a month or apply an event again. `IsAwaitingContinue` and `Continued` expose this presentation lifecycle for UI-19; the other orchestration stages remain in their own tasks.

The hub shortcut opens `PanelId.MonthlyResults = 11` as a read-only consultation. Every visit queries SQLite again. Missing results use `EMPTY_RESULTS` with an explicit absence message; query failures provide retry. No forecast or sample values substitute for a saved result. The cash table uses the existing `TotalCosts`, signed event impact, stored result and opening/closing cash. Loan principal and interest are shown as details already included in the installment. The native chart uses a common absolute scale with outflows to the left of zero. ART-28 layers share the exact viewport, pivot positions, uniform scales and order from `docs/art/layouts/dre-layout.json`.

## Bankruptcy UI lifecycle (UI-16)

`UIStateListener.bankruptcyPanel` opens `Panel_Bankruptcy` only at `GameOver_Bankruptcy`, after the monthly summary's explicit acknowledgement. `BankruptcyController` queries the saved session and its `RoundResultEntity` history on every visit. `BankruptcyPresentation` explains the recorded ending and confirms the three-loss sequence only when all three saved results are negative. Missing or inconsistent history remains explicit; absent indicators are never invented as zero. This projection never determines or changes the session's status. `MonthlySessionSettlement` and its existing three-consecutive-loss rule remain unchanged, including BANKRUPT precedence over COMPLETED in month three and the reset of the counter on a zero or positive result. Negative cash alone does not cause bankruptcy.

Each saved month, the last saved month and the quarterly comparison are read-only consultations. Return restores the source and its keyboard focus, including monthly → quarterly → same month → bankruptcy. Menu remains unavailable outside the hub; Sair uses the existing exit dialog. Business difficulty and CH01 concerned are separate complete, proportionally fitted images; explanations and indicators are native TMP text.

Reload selects the latest finished campaign for the same user without replacing an active or incompatible save. A bankrupt campaign resumes the monthly summary before acknowledgement and its bankruptcy panel afterwards. The existing `finalSummaryAcknowledged` and `finalReportArchived` booleans now cover both ending types; no economic field or enum is added. An archived latest ending never resurrects an earlier conclusion.

New game requires confirmation and uses `StartNewCampaignPreservingReport`, extended to BANKRUPT. It marks only automatic resumption as archived, preserves the session, results, event snapshots and event plans, clears the draft through `PlayerSession.Clear`, then opens identification in the same GameScene. A replacement stays in memory until D3. A failed archive leaves the current campaign intact and offers retry/cancel. Entering bankruptcy never deletes records, and this action does not invoke the destructive legacy `RestartSession` workflow.

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
