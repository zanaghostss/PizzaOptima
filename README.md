# # 🍕 Pizza Optima — Operations Research Command Center

> A WPF desktop app that runs a pizza shop the way an industrial engineer would: every business decision is solved by a hand-built Operations Research engine. **Zero external packages. Every solver written from scratch.**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF%20%2B%20MVVM-FF7A2F)
![Solvers](https://img.shields.io/badge/solvers-hand--built-F2C14E)
![Dependencies](https://img.shields.io/badge/dependencies-zero-7FC26E)

---

## Why this exists

Operations Research courses teach you the Simplex tableau, Erlang's queueing formulas, Wilson's EOQ, and the Hungarian method — usually on paper, one exercise at a time. **Pizza Optima** wires all of them into a single living dashboard for one imaginary pizzeria, so you can *feel* what the math actually does: drag a number, hit ⚡, and watch the optimal decision change.

It is also deliberately minimal in structure: **one View, one ViewModel, one Window.** All the intelligence lives in the algorithms, not the architecture.

---

## The five decision engines

| Tab | Business question | OR technique | Course |
|-----|-------------------|--------------|--------|
| 🔥 **Production Mix** | Which pizzas should we bake today to maximize profit? | Linear Programming (Dantzig Simplex) **+ Branch & Bound** for whole-pizza integer solutions | OR-1 |
| ⏳ **Customer Queue** | How many cashiers keep the line tolerable? | M/M/c steady-state analysis + **Erlang-C** waiting probability | OR-2 |
| 🧀 **Cheese Inventory** | When and how much mozzarella should we order? | **EOQ (Wilson)** + reorder point with safety stock `SS = z·σ·√L` | OR-2 |
| 🛵 **Delivery Dispatch** | Which driver takes which zone? | **Hungarian algorithm, O(n³)** with dual potentials | OR-1 |
| 🎲 **Monte-Carlo Lab** | Does the queueing theory survive contact with randomness? | **Discrete-event simulation** — tens of thousands of synthetic customers vs. the closed-form answers | OR-2 |

### Details worth bragging about

- **Real sensitivity analysis.** The Simplex engine reports **shadow prices** and slack for every resource, so the app literally tells you *"cheese is your bottleneck — one extra gram is worth $0.0067."*
- **True integer optimization.** Toggle one checkbox and the LP relaxation becomes a full **Branch & Bound** tree search (most-fractional branching, bound pruning, box bounds via variable substitution). The UI even shows the node count and the integrality gap.
- **Theory vs. reality.** The simulator samples exponential interarrival/service times, routes each customer to the earliest-free cashier, then prints a side-by-side table: analytic Wq vs. simulated Wq, Erlang-C vs. empirical P(wait). Typical agreement: within a few percent over ~80,000 customers.
- **Tail-risk thinking.** Averages lie. The Monte-Carlo tab reports **P50 / P95 / P99 waits** and a histogram — because ops engineers design to percentiles, not means.
- **Charts with no chart library.** Every bar chart (optimal mix, Wq vs. servers, the convex EOQ cost bowl, the wait histogram) is rendered with a plain XAML `ItemsControl` template.
- **Inverse normal from scratch.** Safety stock needs `z = Φ⁻¹(service level)`; the app implements **Acklam's rational approximation** (matches SciPy to 5 decimals) instead of shipping a stats package.

---

## Quick start

Requires **Windows** with the .NET SDK.

```bash
cd PizzaOptima
dotnet run
```

> Using .NET 8 instead of 10? Open `PizzaOptima.csproj` and change
> `net10.0-windows` → `net8.0-windows`. Nothing else needs to change.

---

## A 60-second demo script

1. **Production Mix** — hit ⚡. Note the Simplex answer suggests *5.56* Veggie pizzas. Tick **Integer solution** and re-solve: Branch & Bound returns whole pizzas (176 Pepperoni + 4 Veggie, $1,966) and shows you exactly how much profit integrality costs.
2. **Customer Queue** — look at the bar chart: going from 3 → 4 cashiers slashes the wait from ~3 min to seconds. Queues are brutally non-linear; the chart makes it visceral.
3. **Monte-Carlo Lab** — run the sim. Watch the simulated Wq land within a few percent of the formula. Then go back to the Queue tab, set λ = 56 (so ρ ≥ 1), and re-run: theory says *unstable*, and the simulation shows you the meltdown.
4. **Cheese Inventory** — the cost bars form a bowl with Q* at the bottom. That's convexity, live.
5. **Delivery Dispatch** — edit any travel time, re-solve, and get the certified optimum out of all 4! = 24 assignments — no brute force, just dual potentials.

---

## Project structure

```
PizzaOptima/
├── PizzaOptima.csproj      # net10.0-windows, UseWPF, nothing else
├── App.xaml / App.xaml.cs  # standard WPF entry point
├── MainWindow.xaml         # the single View — 5 tabs, dark ember theme, XAML-only charts
├── MainWindow.xaml.cs      # 10 lines: wires the ViewModel
└── MainViewModel.cs        # everything else:
    ├── MainViewModel       #   bindings + commands for all 5 tabs
    ├── Simplex             #   tableau Simplex, Dantzig rule, duals & slacks
    ├── BranchAndBound      #   ILP via B&B on top of the Simplex relaxation
    ├── Mmc                 #   M/M/c steady state + Erlang-C
    ├── Hungarian           #   O(n³) assignment with potentials (u, v)
    ├── QueueSim            #   Monte-Carlo discrete-event simulator
    └── Stats               #   Acklam inverse-normal approximation
```

MVVM, but honest MVVM: no frameworks, no dependency injection, no mediator — a `RelayCommand`, `INotifyPropertyChanged`, and data binding.

---

## Verification

Every engine was cross-checked against independent reference solvers before shipping:

| Engine | Reference | Result |
|--------|-----------|--------|
| Simplex | HiGHS (`scipy.optimize.linprog`) | Identical x*, Z*, shadow prices, slacks |
| Branch & Bound | `scipy.optimize.milp` | Exact match on the app's instance **and 6/6 random ILPs** |
| Hungarian | Exhaustive search over all permutations | Identical optimum |
| M/M/c + Erlang-C | Closed-form recomputation | Identical to machine precision |
| Monte-Carlo DES | Analytic M/M/c | Converges to theory (~few % at 2,000 simulated hours) |
| Inverse normal | `scipy.stats.norm.ppf` | Matches to 5 decimal places |

---

## Things to try

- Make the oven the bottleneck (drop oven minutes to 600) and watch the shadow prices flip.
- Set the service level to 99.9% and see what tail risk does to your safety stock.
- Change the Monte-Carlo seed — same parameters, different universe, same long-run averages. That's the law of large numbers earning its paycheck.

---

*Built as a one-View, one-ViewModel showcase of OR-1 & OR-2 — proof that a "simple" app can have a serious mathematical engine room.* 🍕📐
