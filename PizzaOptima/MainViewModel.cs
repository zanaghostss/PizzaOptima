using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;

namespace PizzaOptima
{
    // ════════════════════════════════════════════════════════════════════
    //  Pizza Optima — Operations Research Command Center
    //  OR-1 : Linear Programming (Simplex) · Integer LP (Branch & Bound)
    //         Assignment Problem (Hungarian, O(n³))
    //  OR-2 : Queueing Theory (M/M/c + Erlang-C) · Inventory (EOQ + ROP)
    //         Monte-Carlo Discrete-Event Simulation (theory vs. reality)
    //  Every solver implemented from scratch — zero external packages.
    // ════════════════════════════════════════════════════════════════════
    public class MainViewModel : INotifyPropertyChanged
    {
        private const double BarMaxWidth = 330;
        private static readonly Brush FlameBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x2F));
        private static readonly Brush CheeseBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xC1, 0x4E));
        private static readonly Brush BasilBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0xC2, 0x6E));
        private static readonly Brush EmberBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x4B, 0x31));

        public MainViewModel()
        {
            SolveLpCommand = new RelayCommand(SolveLp);
            SolveQueueCommand = new RelayCommand(SolveQueue);
            SolveEoqCommand = new RelayCommand(SolveEoq);
            SolveAssignmentCommand = new RelayCommand(SolveAssignment);
            RunSimCommand = new RelayCommand(RunSimulation);

            SolveLp(); SolveQueue(); SolveEoq(); SolveAssignment(); RunSimulation();
        }

        // ─────────────────────────────────────────────────────────────
        //  TAB 1 · Optimal Production Mix — LP (Simplex) or ILP (B&B)
        //  Max Z = Σ profitⱼ·xⱼ   s.t.   resource usage ≤ availability
        // ─────────────────────────────────────────────────────────────
        public ObservableCollection<PizzaRow> Pizzas { get; } = new()
        {
            new PizzaRow { Name = "The Works",  Profit = 9.5,  Dough = 280, Cheese = 120, Sauce = 80, OvenMin = 12 },
            new PizzaRow { Name = "Pepperoni",  Profit = 11.0, Dough = 250, Cheese = 140, Sauce = 70, OvenMin = 10 },
            new PizzaRow { Name = "Veggie",     Profit = 7.5,  Dough = 260, Cheese = 90,  Sauce = 90, OvenMin = 9  },
            new PizzaRow { Name = "Margherita", Profit = 6.0,  Dough = 240, Cheese = 150, Sauce = 60, OvenMin = 8  },
        };

        private double _doughAvail = 50000, _cheeseAvail = 25000, _sauceAvail = 15000, _ovenAvail = 1800;
        public double DoughAvail { get => _doughAvail; set { _doughAvail = value; OnPropertyChanged(); } }
        public double CheeseAvail { get => _cheeseAvail; set { _cheeseAvail = value; OnPropertyChanged(); } }
        public double SauceAvail { get => _sauceAvail; set { _sauceAvail = value; OnPropertyChanged(); } }
        public double OvenAvail { get => _ovenAvail; set { _ovenAvail = value; OnPropertyChanged(); } }

        private bool _integerMode = true;
        public bool IntegerMode { get => _integerMode; set { _integerMode = value; OnPropertyChanged(); } }

        private string _lpResult = "";
        public string LpResult { get => _lpResult; set { _lpResult = value; OnPropertyChanged(); } }

        private ObservableCollection<BarItem> _lpBars = new();
        public ObservableCollection<BarItem> LpBars { get => _lpBars; set { _lpBars = value; OnPropertyChanged(); } }

        public ICommand SolveLpCommand { get; }

        private void SolveLp()
        {
            try
            {
                int n = Pizzas.Count;
                string[] resNames = { "Dough (g)", "Cheese (g)", "Sauce (g)", "Oven time (min)" };
                double[] b = { DoughAvail, CheeseAvail, SauceAvail, OvenAvail };
                var c = Pizzas.Select(p => p.Profit).ToArray();
                var A = new double[4, n];
                for (int j = 0; j < n; j++)
                {
                    A[0, j] = Pizzas[j].Dough; A[1, j] = Pizzas[j].Cheese;
                    A[2, j] = Pizzas[j].Sauce; A[3, j] = Pizzas[j].OvenMin;
                }

                // LP relaxation — always solved (gives duals + the B&B root node)
                var (xr, zr, slack, shadow) = Simplex.Maximize(c, A, b);

                double[] x; double z; int nodes = 0; bool isInt = IntegerMode;
                if (IntegerMode)
                {
                    var ilp = BranchAndBound.Solve(c, A, b, out nodes);
                    if (ilp is null) { LpResult = "ILP infeasible."; return; }
                    (x, z) = ilp.Value;
                }
                else { x = xr; z = zr; }

                var sb = new StringBuilder();
                sb.AppendLine(isInt
                    ? $"◤ INTEGER OPTIMUM — Branch & Bound ({nodes} nodes explored) ◢"
                    : "◤ LP OPTIMUM — Dantzig Simplex ◢");
                sb.AppendLine();
                for (int j = 0; j < n; j++)
                    sb.AppendLine($"   {Pizzas[j].Name,-12} →  {x[j]:0.##} pizzas / day");
                sb.AppendLine();
                sb.AppendLine($"💰 Max daily profit:  Z* = ${z:N2}");
                if (isInt)
                    sb.AppendLine($"   LP relaxation bound: ${zr:N2}  (integrality gap: ${zr - z:0.00})");
                sb.AppendLine();
                sb.AppendLine("── Sensitivity (duals of the LP relaxation) ──");
                for (int i = 0; i < 4; i++)
                {
                    string status = slack[i] > 1e-6
                        ? $"slack {slack[i]:0.#} → not binding"
                        : "BINDING — bottleneck!";
                    sb.AppendLine($"   {resNames[i],-16} shadow ${shadow[i]:0.###}/unit | {status}");
                }
                sb.AppendLine();
                sb.AppendLine("A shadow price tells you what one extra unit of that resource");
                sb.AppendLine("is worth in profit — invest in the bottlenecks first.");
                LpResult = sb.ToString();

                // chart: optimal quantities
                double max = Math.Max(x.Max(), 1e-9);
                var bars = new ObservableCollection<BarItem>();
                for (int j = 0; j < n; j++)
                    bars.Add(new BarItem
                    {
                        Label = Pizzas[j].Name,
                        Width = x[j] / max * BarMaxWidth,
                        Display = $"{x[j]:0.##}",
                        Fill = x[j] == max ? CheeseBrush : FlameBrush
                    });
                LpBars = bars;
            }
            catch (Exception ex) { LpResult = "Error: " + ex.Message; }
        }

        // ─────────────────────────────────────────────────────────────
        //  TAB 2 · Customer Queue — M/M/c with Erlang-C
        // ─────────────────────────────────────────────────────────────
        private double _lambda = 42, _mu = 18, _targetWqMin = 3;
        private int _servers = 3;
        public double Lambda { get => _lambda; set { _lambda = value; OnPropertyChanged(); } }
        public double Mu { get => _mu; set { _mu = value; OnPropertyChanged(); } }
        public int Servers { get => _servers; set { _servers = value; OnPropertyChanged(); } }
        public double TargetWqMin { get => _targetWqMin; set { _targetWqMin = value; OnPropertyChanged(); } }

        private string _queueResult = "";
        public string QueueResult { get => _queueResult; set { _queueResult = value; OnPropertyChanged(); } }

        private ObservableCollection<BarItem> _queueBars = new();
        public ObservableCollection<BarItem> QueueBars { get => _queueBars; set { _queueBars = value; OnPropertyChanged(); } }

        public ICommand SolveQueueCommand { get; }

        private void SolveQueue()
        {
            try
            {
                var sb = new StringBuilder();
                var m = Mmc.Compute(Lambda, Mu, Servers);
                sb.AppendLine($"◤ M/M/{Servers} STEADY-STATE ANALYSIS ◢");
                sb.AppendLine();
                if (m is null)
                {
                    sb.AppendLine("⚠ UNSTABLE SYSTEM (ρ ≥ 1) — the queue grows without bound.");
                    sb.AppendLine("Add cashiers or speed up service.");
                }
                else
                {
                    sb.AppendLine($"   Utilization            ρ  = {m.Rho:P1}");
                    sb.AppendLine($"   P(empty system)        P₀ = {m.P0:P2}");
                    sb.AppendLine($"   P(wait) — Erlang-C     Pw = {m.Pw:P1}");
                    sb.AppendLine($"   Avg queue length       Lq = {m.Lq:0.##} customers");
                    sb.AppendLine($"   Avg in system          L  = {m.L:0.##} customers");
                    sb.AppendLine($"   Avg wait in queue      Wq = {m.Wq * 60:0.##} min");
                    sb.AppendLine($"   Avg time in system     W  = {m.W * 60:0.##} min");
                }
                sb.AppendLine();

                int best = -1;
                for (int cc = 1; cc <= 30; cc++)
                {
                    var t = Mmc.Compute(Lambda, Mu, cc);
                    if (t is not null && t.Wq * 60 <= TargetWqMin) { best = cc; break; }
                }
                sb.AppendLine("── Staffing recommendation ──");
                sb.AppendLine(best > 0
                    ? $"To keep waits ≤ {TargetWqMin:0.#} min you need at least {best} cashier(s)."
                    : "Even 30 cashiers can't hit that target — increase μ instead.");
                sb.AppendLine();
                sb.AppendLine("The chart shows the brutal non-linearity of queues:");
                sb.AppendLine("each extra server below saturation buys an enormous wait reduction.");
                QueueResult = sb.ToString();

                // chart: Wq vs number of servers
                var vals = new List<(int c, double wq)>();
                for (int cc = 1; cc <= 8; cc++)
                {
                    var t = Mmc.Compute(Lambda, Mu, cc);
                    vals.Add((cc, t?.Wq * 60 ?? double.PositiveInfinity));
                }
                double maxFinite = vals.Where(v => !double.IsInfinity(v.wq)).Select(v => v.wq).DefaultIfEmpty(1).Max();
                var bars = new ObservableCollection<BarItem>();
                foreach (var (cc, wq) in vals)
                    bars.Add(new BarItem
                    {
                        Label = $"c = {cc}",
                        Width = double.IsInfinity(wq) ? BarMaxWidth : Math.Max(2, wq / Math.Max(maxFinite, 1e-9) * BarMaxWidth),
                        Display = double.IsInfinity(wq) ? "∞ (unstable)" : $"{wq:0.##} min",
                        Fill = double.IsInfinity(wq) ? EmberBrush : (cc == Servers ? CheeseBrush : FlameBrush)
                    });
                QueueBars = bars;
            }
            catch (Exception ex) { QueueResult = "Error: " + ex.Message; }
        }

        // ─────────────────────────────────────────────────────────────
        //  TAB 3 · Mozzarella Inventory — EOQ + Reorder Point
        // ─────────────────────────────────────────────────────────────
        private double _annualDemand = 14600, _orderCost = 90, _holdingCost = 2.2,
                       _leadTimeDays = 4, _workingDays = 365, _dailyStd = 9, _serviceLevel = 95;
        public double AnnualDemand { get => _annualDemand; set { _annualDemand = value; OnPropertyChanged(); } }
        public double OrderCost { get => _orderCost; set { _orderCost = value; OnPropertyChanged(); } }
        public double HoldingCost { get => _holdingCost; set { _holdingCost = value; OnPropertyChanged(); } }
        public double LeadTimeDays { get => _leadTimeDays; set { _leadTimeDays = value; OnPropertyChanged(); } }
        public double WorkingDays { get => _workingDays; set { _workingDays = value; OnPropertyChanged(); } }
        public double DailyStd { get => _dailyStd; set { _dailyStd = value; OnPropertyChanged(); } }
        public double ServiceLevel { get => _serviceLevel; set { _serviceLevel = value; OnPropertyChanged(); } }

        private string _eoqResult = "";
        public string EoqResult { get => _eoqResult; set { _eoqResult = value; OnPropertyChanged(); } }

        private ObservableCollection<BarItem> _eoqBars = new();
        public ObservableCollection<BarItem> EoqBars { get => _eoqBars; set { _eoqBars = value; OnPropertyChanged(); } }

        public ICommand SolveEoqCommand { get; }

        private void SolveEoq()
        {
            try
            {
                double D = AnnualDemand, S = OrderCost, H = HoldingCost;
                if (D <= 0 || S <= 0 || H <= 0 || WorkingDays <= 0)
                { EoqResult = "All inputs must be positive."; return; }

                double q = Math.Sqrt(2 * D * S / H);                 // Wilson formula
                double nOrders = D / q;
                double cycleDays = WorkingDays / nOrders;
                double dailyDemand = D / WorkingDays;
                double z = Stats.NormInv(Math.Clamp(ServiceLevel, 50, 99.99) / 100.0);
                double ss = z * DailyStd * Math.Sqrt(LeadTimeDays);  // safety stock
                double rop = dailyDemand * LeadTimeDays + ss;
                double TC(double Q) => (D / Q) * S + (Q / 2 + ss) * H;

                var sb = new StringBuilder();
                sb.AppendLine("◤ EOQ MODEL (Wilson) — Mozzarella ◢");
                sb.AppendLine();
                sb.AppendLine($"   Economic order qty   Q*  = {q:0.#} kg");
                sb.AppendLine($"   Orders per year      N   = {nOrders:0.#}");
                sb.AppendLine($"   Order cycle          T   = {cycleDays:0.#} days");
                sb.AppendLine();
                sb.AppendLine($"   Safety stock  SS = z·σ·√L = {z:0.##} × {DailyStd:0.#} × √{LeadTimeDays:0.#} = {ss:0.#} kg");
                sb.AppendLine($"   Reorder point ROP = d̄·L + SS = {rop:0.#} kg");
                sb.AppendLine();
                sb.AppendLine($"💰 Total annual inventory cost ≈ ${TC(q):N0}");
                sb.AppendLine();
                sb.AppendLine($"Policy: when stock hits {rop:0.#} kg, order {q:0.#} kg.");
                sb.AppendLine($"At {ServiceLevel:0.#}% service level you almost never run dry mid-shift.");
                sb.AppendLine();
                sb.AppendLine("The chart proves Q* sits at the bottom of the convex cost curve —");
                sb.AppendLine("order more OR less than Q* and total cost rises.");
                EoqResult = sb.ToString();

                // chart: convex total-cost curve around Q*
                double[] mult = { 0.5, 0.7, 0.85, 1.0, 1.15, 1.3, 1.5 };
                double maxTc = mult.Select(f => TC(q * f)).Max();
                var bars = new ObservableCollection<BarItem>();
                foreach (double f in mult)
                {
                    double tc = TC(q * f);
                    bars.Add(new BarItem
                    {
                        Label = f == 1.0 ? "Q*  ★" : $"{f:0.0#}·Q*",
                        Width = tc / maxTc * BarMaxWidth,
                        Display = $"${tc:N0}",
                        Fill = f == 1.0 ? BasilBrush : FlameBrush
                    });
                }
                EoqBars = bars;
            }
            catch (Exception ex) { EoqResult = "Error: " + ex.Message; }
        }

        // ─────────────────────────────────────────────────────────────
        //  TAB 4 · Delivery Dispatch — Hungarian Algorithm
        // ─────────────────────────────────────────────────────────────
        public ObservableCollection<DriverRow> Drivers { get; } = new()
        {
            new DriverRow { Name = "Alex",  Z1 = 14, Z2 = 8,  Z3 = 21, Z4 = 11 },
            new DriverRow { Name = "Blake", Z1 = 9,  Z2 = 12, Z3 = 16, Z4 = 18 },
            new DriverRow { Name = "Casey", Z1 = 17, Z2 = 15, Z3 = 7,  Z4 = 13 },
            new DriverRow { Name = "Drew",  Z1 = 12, Z2 = 19, Z3 = 10, Z4 = 6  },
        };

        private string _assignResult = "";
        public string AssignResult { get => _assignResult; set { _assignResult = value; OnPropertyChanged(); } }

        public ICommand SolveAssignmentCommand { get; }

        private void SolveAssignment()
        {
            try
            {
                int n = Drivers.Count;
                var cost = new double[n, n];
                for (int i = 0; i < n; i++)
                {
                    cost[i, 0] = Drivers[i].Z1; cost[i, 1] = Drivers[i].Z2;
                    cost[i, 2] = Drivers[i].Z3; cost[i, 3] = Drivers[i].Z4;
                }

                int[] assign = Hungarian.Solve(cost);
                string[] zones = { "North", "East", "South", "West" };

                var sb = new StringBuilder();
                sb.AppendLine("◤ OPTIMAL DISPATCH — Hungarian Algorithm O(n³) ◢");
                sb.AppendLine();
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    double t = cost[i, assign[i]];
                    total += t;
                    sb.AppendLine($"   🛵 {Drivers[i].Name,-7} → {zones[assign[i]],-6} ({t:0.#} min)");
                }
                sb.AppendLine();
                sb.AppendLine($"⏱ Minimum total delivery time: {total:0.#} min");
                sb.AppendLine();
                sb.AppendLine("Out of all 4! = 24 possible assignments, this one is provably");
                sb.AppendLine("optimal — certified by the dual potentials (u, v), no brute force.");
                AssignResult = sb.ToString();
            }
            catch (Exception ex) { AssignResult = "Error: " + ex.Message; }
        }

        // ─────────────────────────────────────────────────────────────
        //  TAB 5 · Monte-Carlo Discrete-Event Simulation
        //  Validates the analytic M/M/c against a simulated rush
        // ─────────────────────────────────────────────────────────────
        private double _simHours = 2000;
        private int _simSeed = 42;
        public double SimHours { get => _simHours; set { _simHours = value; OnPropertyChanged(); } }
        public int SimSeed { get => _simSeed; set { _simSeed = value; OnPropertyChanged(); } }

        private string _simResult = "";
        public string SimResult { get => _simResult; set { _simResult = value; OnPropertyChanged(); } }

        private ObservableCollection<BarItem> _simBars = new();
        public ObservableCollection<BarItem> SimBars { get => _simBars; set { _simBars = value; OnPropertyChanged(); } }

        public ICommand RunSimCommand { get; }

        private void RunSimulation()
        {
            try
            {
                if (SimHours <= 0) { SimResult = "Sim hours must be positive."; return; }
                var sim = QueueSim.Run(Lambda, Mu, Servers, SimHours, SimSeed);
                var theory = Mmc.Compute(Lambda, Mu, Servers);

                var sb = new StringBuilder();
                sb.AppendLine($"◤ MONTE-CARLO DES — {sim.Served:N0} customers simulated over {SimHours:N0} h ◢");
                sb.AppendLine();
                sb.AppendLine("                      THEORY (M/M/c)     SIMULATION");
                if (theory is not null)
                {
                    sb.AppendLine($"   Avg wait Wq        {theory.Wq * 60,8:0.###} min     {sim.AvgWqMin,8:0.###} min");
                    sb.AppendLine($"   Utilization ρ      {theory.Rho,8:P1}       {sim.Utilization,8:P1}");
                    sb.AppendLine($"   P(wait > 0)        {theory.Pw,8:P1}       {sim.PWait,8:P1}");
                    double err = Math.Abs(sim.AvgWqMin - theory.Wq * 60) / Math.Max(theory.Wq * 60, 1e-9);
                    sb.AppendLine();
                    sb.AppendLine($"   Relative error on Wq: {err:P2} — the math holds up. ✔");
                }
                else
                {
                    sb.AppendLine("   Theory: UNSTABLE (ρ ≥ 1) — simulation shows the meltdown:");
                    sb.AppendLine($"   Simulated avg wait: {sim.AvgWqMin:0.##} min (and growing with sim length)");
                }
                sb.AppendLine();
                sb.AppendLine("── Tail risk (what theory averages hide) ──");
                sb.AppendLine($"   Median wait  P50 = {sim.P50Min:0.##} min");
                sb.AppendLine($"   P95 wait         = {sim.P95Min:0.##} min");
                sb.AppendLine($"   P99 wait         = {sim.P99Min:0.##} min");
                sb.AppendLine($"   Worst customer   = {sim.MaxMin:0.##} min");
                sb.AppendLine();
                sb.AppendLine("Averages look fine — but 1 in 20 customers eats the P95 wait.");
                sb.AppendLine("That's why ops engineers design to percentiles, not means.");
                SimResult = sb.ToString();

                // chart: wait-time histogram
                double maxCount = Math.Max(sim.Hist.Max(), 1);
                var bars = new ObservableCollection<BarItem>();
                for (int i = 0; i < sim.Hist.Length; i++)
                    bars.Add(new BarItem
                    {
                        Label = $"{sim.BinEdges[i]:0.#}–{sim.BinEdges[i + 1]:0.#}m",
                        Width = Math.Max(2, sim.Hist[i] / maxCount * BarMaxWidth),
                        Display = $"{sim.Hist[i]:N0}",
                        Fill = i == 0 ? BasilBrush : FlameBrush
                    });
                SimBars = bars;
            }
            catch (Exception ex) { SimResult = "Error: " + ex.Message; }
        }

        // ─── INotifyPropertyChanged ───
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ════════════════ row models & chart bars ════════════════
    public class PizzaRow
    {
        public string Name { get; set; } = "";
        public double Profit { get; set; }
        public double Dough { get; set; }
        public double Cheese { get; set; }
        public double Sauce { get; set; }
        public double OvenMin { get; set; }
    }

    public class DriverRow
    {
        public string Name { get; set; } = "";
        public double Z1 { get; set; }
        public double Z2 { get; set; }
        public double Z3 { get; set; }
        public double Z4 { get; set; }
    }

    public class BarItem
    {
        public string Label { get; set; } = "";
        public double Width { get; set; }
        public string Display { get; set; } = "";
        public Brush Fill { get; set; } = Brushes.Orange;
    }

    // ════════════════ Simplex engine (OR-1) ════════════════
    // Max c·x  s.t.  Ax ≤ b , x ≥ 0 , b ≥ 0 — standard tableau, Dantzig rule
    public static class Simplex
    {
        public static (double[] x, double z, double[] slack, double[] shadow)
            Maximize(double[] c, double[,] A, double[] b)
        {
            int m = b.Length, n = c.Length;
            int cols = n + m + 1;
            var T = new double[m + 1, cols];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) T[i, j] = A[i, j];
                T[i, n + i] = 1.0;                       // slack variable
                T[i, cols - 1] = b[i];
            }
            for (int j = 0; j < n; j++) T[m, j] = -c[j]; // Z-row

            var basis = Enumerable.Range(n, m).ToArray();

            for (int iter = 0; iter < 800; iter++)
            {
                int pc = -1; double most = -1e-9;
                for (int j = 0; j < cols - 1; j++)
                    if (T[m, j] < most) { most = T[m, j]; pc = j; }
                if (pc < 0) break;                       // optimal

                int pr = -1; double best = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                    if (T[i, pc] > 1e-9)
                    {
                        double ratio = T[i, cols - 1] / T[i, pc];
                        if (ratio < best - 1e-12) { best = ratio; pr = i; }
                    }
                if (pr < 0) throw new Exception("LP is unbounded.");

                double piv = T[pr, pc];
                for (int j = 0; j < cols; j++) T[pr, j] /= piv;
                for (int i = 0; i <= m; i++)
                {
                    if (i == pr) continue;
                    double f = T[i, pc];
                    if (Math.Abs(f) < 1e-12) continue;
                    for (int j = 0; j < cols; j++) T[i, j] -= f * T[pr, j];
                }
                basis[pr] = pc;
            }

            var x = new double[n];
            var slack = new double[m];
            for (int i = 0; i < m; i++)
            {
                if (basis[i] < n) x[basis[i]] = T[i, cols - 1];
                else slack[basis[i] - n] = T[i, cols - 1];
            }
            var shadow = new double[m];
            for (int i = 0; i < m; i++) shadow[i] = T[m, n + i];

            return (x, T[m, cols - 1], slack, shadow);
        }
    }

    // ════════════════ Branch & Bound ILP (OR-1) ════════════════
    // Integer programming on top of the Simplex relaxation.
    // Assumes A ≥ 0 (true for any consumption/production model).
    public static class BranchAndBound
    {
        public static (double[] x, double z)? Solve(double[] c, double[,] A, double[] b, out int nodes)
        {
            int n = c.Length, m = b.Length;
            nodes = 0;

            // natural upper bounds from resource limits: ub_j = min_i b_i / A_ij
            var ub0 = new double[n];
            for (int j = 0; j < n; j++)
            {
                double u = 1e6;
                for (int i = 0; i < m; i++)
                    if (A[i, j] > 1e-12) u = Math.Min(u, b[i] / A[i, j]);
                ub0[j] = Math.Floor(Math.Max(0, u));
            }

            double bestZ = double.NegativeInfinity;
            double[]? bestX = null;
            var stack = new Stack<(double[] lb, double[] ub)>();
            stack.Push((new double[n], ub0));

            while (stack.Count > 0 && nodes < 20000)
            {
                nodes++;
                var (lb, ub) = stack.Pop();
                var rel = SolveBounded(c, A, b, lb, ub);
                if (rel is null) continue;                       // infeasible node
                var (x, z) = rel.Value;
                if (z <= bestZ + 1e-6) continue;                 // pruned by bound

                // pick the most fractional variable (closest to .5)
                int branch = -1; double fracness = 1e-6;
                for (int j = 0; j < n; j++)
                {
                    double f = x[j] - Math.Floor(x[j]);
                    double dist = Math.Min(f, 1 - f);
                    if (dist > fracness) { fracness = dist; branch = j; }
                }
                if (branch < 0)                                  // integral → incumbent
                {
                    bestZ = z;
                    bestX = x.Select(v => Math.Round(v)).ToArray();
                    continue;
                }

                double fl = Math.Floor(x[branch]), ce = Math.Ceiling(x[branch]);
                var ubL = (double[])ub.Clone(); ubL[branch] = Math.Min(ub[branch], fl);
                var lbR = (double[])lb.Clone(); lbR[branch] = Math.Max(lb[branch], ce);
                if (ubL[branch] >= lb[branch] - 1e-9) stack.Push((lb, ubL));   // x ≤ ⌊x⌋
                if (lbR[branch] <= ub[branch] + 1e-9) stack.Push((lbR, ub));   // x ≥ ⌈x⌉
            }

            return bestX is null ? null : (bestX, bestZ);
        }

        // LP with box bounds lb ≤ x ≤ ub via substitution y = x − lb
        private static (double[] x, double z)? SolveBounded(
            double[] c, double[,] A, double[] b, double[] lb, double[] ub)
        {
            int n = c.Length, m = b.Length;
            for (int j = 0; j < n; j++) if (ub[j] < lb[j] - 1e-9) return null;

            var rows = new List<double[]>();
            var rhs = new List<double>();
            for (int i = 0; i < m; i++)
            {
                double r = b[i];
                var row = new double[n];
                for (int j = 0; j < n; j++) { row[j] = A[i, j]; r -= A[i, j] * lb[j]; }
                if (r < -1e-9) return null;        // valid prune since A ≥ 0
                rows.Add(row); rhs.Add(Math.Max(0, r));
            }
            for (int j = 0; j < n; j++)            // explicit upper-bound rows
            {
                double span = ub[j] - lb[j];
                if (span < 1e5)
                {
                    var row = new double[n]; row[j] = 1;
                    rows.Add(row); rhs.Add(Math.Max(0, span));
                }
            }

            var A2 = new double[rows.Count, n];
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < n; j++) A2[i, j] = rows[i][j];

            try
            {
                var (y, zy, _, _) = Simplex.Maximize(c, A2, rhs.ToArray());
                var x = new double[n]; double z = zy;
                for (int j = 0; j < n; j++) { x[j] = y[j] + lb[j]; z += c[j] * lb[j]; }
                return (x, z);
            }
            catch { return null; }
        }
    }

    // ════════════════ M/M/c queue engine (OR-2) ════════════════
    public static class Mmc
    {
        public record Result(double Rho, double P0, double Pw, double Lq, double L, double Wq, double W);

        public static Result? Compute(double lambda, double mu, int c)
        {
            if (lambda <= 0 || mu <= 0 || c < 1) return null;
            double a = lambda / mu;                 // offered load (Erlangs)
            double rho = a / c;
            if (rho >= 1.0) return null;            // unstable

            double sum = 0, term = 1;               // running a^n / n!
            for (int n = 0; n < c; n++) { sum += term; term *= a / (n + 1); }
            // after loop: term = a^c / c!
            double tail = term / (1 - rho);
            double p0 = 1.0 / (sum + tail);

            double pw = tail * p0;                  // Erlang-C: P(wait > 0)
            double lq = pw * rho / (1 - rho);
            double wq = lq / lambda;
            double w = wq + 1.0 / mu;
            double l = lambda * w;
            return new Result(rho, p0, pw, lq, l, wq, w);
        }
    }

    // ════════════════ Hungarian algorithm O(n³) (OR-1) ════════════════
    public static class Hungarian
    {
        public static int[] Solve(double[,] a)
        {
            int n = a.GetLength(0);
            var u = new double[n + 1];
            var v = new double[n + 1];
            var p = new int[n + 1];
            var way = new int[n + 1];

            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                var minv = new double[n + 1];
                var used = new bool[n + 1];
                for (int j = 0; j <= n; j++) minv[j] = double.PositiveInfinity;

                do
                {
                    used[j0] = true;
                    int i0 = p[j0], j1 = -1;
                    double delta = double.PositiveInfinity;
                    for (int j = 1; j <= n; j++)
                        if (!used[j])
                        {
                            double cur = a[i0 - 1, j - 1] - u[i0] - v[j];
                            if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                            if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                        }
                    for (int j = 0; j <= n; j++)
                    {
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);

                do { int j1 = way[j0]; p[j0] = p[j1]; j0 = j1; } while (j0 != 0);
            }

            var result = new int[n];                // result[driver] = zone
            for (int j = 1; j <= n; j++)
                if (p[j] > 0) result[p[j] - 1] = j - 1;
            return result;
        }
    }

    // ════════════════ Monte-Carlo discrete-event simulator (OR-2) ════════════════
    // Exact FCFS M/M/c sample-path simulation: exponential interarrivals &
    // service times, customers grab the earliest-free server.
    public static class QueueSim
    {
        public record Result(double AvgWqMin, double P50Min, double P95Min, double P99Min,
                             double MaxMin, double Utilization, double PWait,
                             int Served, double[] Hist, double[] BinEdges);

        public static Result Run(double lambda, double mu, int c, double hours, int seed)
        {
            var rng = new Random(seed);
            double Exp(double rate) => -Math.Log(1.0 - rng.NextDouble()) / rate;

            var freeAt = new double[Math.Max(1, c)];
            var waits = new List<double>();
            double t = 0, busy = 0;
            int waited = 0;

            while (true)
            {
                t += Exp(lambda);
                if (t > hours) break;

                int k = 0;
                for (int i = 1; i < freeAt.Length; i++)
                    if (freeAt[i] < freeAt[k]) k = i;

                double start = Math.Max(t, freeAt[k]);
                double w = start - t;
                if (w > 1e-12) waited++;
                waits.Add(w * 60);                  // store in minutes

                double s = Exp(mu);
                freeAt[k] = start + s;
                busy += s;
            }

            if (waits.Count == 0)
                return new Result(0, 0, 0, 0, 0, 0, 0, 0, new double[12], new double[13]);

            waits.Sort();
            double Pct(double p) => waits[Math.Min(waits.Count - 1, (int)(p * waits.Count))];
            double avg = waits.Average();
            double p99 = Pct(0.99);

            // histogram: 12 bins up to P99 (last bin absorbs the tail)
            int bins = 12;
            double range = Math.Max(p99, 1e-6);
            var hist = new double[bins];
            var edges = new double[bins + 1];
            for (int i = 0; i <= bins; i++) edges[i] = range * i / bins;
            foreach (double w in waits)
            {
                int idx = Math.Min(bins - 1, (int)(w / range * bins));
                hist[idx]++;
            }

            return new Result(avg, Pct(0.50), Pct(0.95), p99,
                              waits[^1], busy / (freeAt.Length * hours),
                              (double)waited / waits.Count, waits.Count, hist, edges);
        }
    }

    // ════════════════ Stats: inverse standard normal (Acklam) ════════════════
    public static class Stats
    {
        public static double NormInv(double pr)
        {
            double[] aa = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                            1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
            double[] bb = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                            6.680131188771972e+01, -1.328068155288572e+01 };
            double[] cc = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                            -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
            double[] dd = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                            3.754408661907416e+00 };
            const double pl = 0.02425, ph = 1 - pl;
            double q, r;
            if (pr < pl)
            {
                q = Math.Sqrt(-2 * Math.Log(pr));
                return (((((cc[0] * q + cc[1]) * q + cc[2]) * q + cc[3]) * q + cc[4]) * q + cc[5]) /
                       ((((dd[0] * q + dd[1]) * q + dd[2]) * q + dd[3]) * q + 1);
            }
            if (pr <= ph)
            {
                q = pr - 0.5; r = q * q;
                return (((((aa[0] * r + aa[1]) * r + aa[2]) * r + aa[3]) * r + aa[4]) * r + aa[5]) * q /
                       (((((bb[0] * r + bb[1]) * r + bb[2]) * r + bb[3]) * r + bb[4]) * r + 1);
            }
            q = Math.Sqrt(-2 * Math.Log(1 - pr));
            return -(((((cc[0] * q + cc[1]) * q + cc[2]) * q + cc[3]) * q + cc[4]) * q + cc[5]) /
                     ((((dd[0] * q + dd[1]) * q + dd[2]) * q + dd[3]) * q + 1);
        }
    }

    // ════════════════ RelayCommand ════════════════
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
