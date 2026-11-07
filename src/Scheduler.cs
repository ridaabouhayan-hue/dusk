using System;
using System.Windows.Forms;

namespace Dusk
{
    // De timer: wanneer moet welke actie gebeuren? Tikt twee keer per seconde.
    public class Scheduler : IDisposable
    {
        public bool Active { get; private set; }
        public PowerAction Action { get; private set; }
        public DateTime Target { get; private set; }     // lokale tijd
        public DateTime Started { get; private set; }
        public bool Warned { get; private set; }

        public event EventHandler Changed;   // gestart, gestopt, tijd erbij
        public event EventHandler Tick;      // elke halve seconde zolang hij loopt
        public event EventHandler Warn;      // de waarschuwing vooraf
        public event EventHandler Due;       // nu uitvoeren
        public event EventHandler Resumed;   // de pc sliep en de tijd is voorbij: gestopt in plaats van uitgevoerd

        readonly Timer timer = new Timer();
        DateTime lastTick;
        public Func<DateTime> Clock = delegate { return DateTime.Now; };

        public Scheduler()
        {
            timer.Interval = 500;
            timer.Tick += delegate { OnTick(); };
        }

        public void Start(PowerAction a, DateTime target)
        {
            Action = a; Target = target; Started = Clock(); Active = true;
            // Een timer korter dan de waarschuwingstijd: je ziet het aftellen al, dus geen extra melding
            Warned = (target - Started).TotalSeconds <= Settings.Current.WarnMinutes * 60 + 1;
            lastTick = Started;
            timer.Start();
            Log.Info("Timer: " + a + " om " + target.ToString("HH:mm:ss"));
            Raise(Changed);
        }

        public void Cancel()
        {
            if (!Active) return;
            Active = false; timer.Stop();
            Log.Info("Timer gestopt");
            Raise(Changed);
        }

        public void Add(int minutes)
        {
            if (!Active) return;
            DateTime now = Clock();
            DateTime basis = Target < now ? now : Target;
            Target = basis.AddMinutes(minutes);
            if (Remaining.TotalMinutes > Settings.Current.WarnMinutes) Warned = false;
            Raise(Changed);
        }

        public TimeSpan Remaining { get { return Active ? Target - Clock() : TimeSpan.Zero; } }

        // Hoeveel van de tijd is er nog over (1 = net gestart, 0 = nu)
        public float Fraction
        {
            get
            {
                if (!Active) return 1f;
                double total = (Target - Started).TotalSeconds;
                if (total <= 0) return 0f;
                return (float)Math.Max(0, Math.Min(1, Remaining.TotalSeconds / total));
            }
        }

        // Los van de klok te testen: wat moet er gebeuren op dit moment?
        public void OnTick()
        {
            if (!Active) return;
            DateTime now = Clock();
            double gap = (now - lastTick).TotalSeconds;
            lastTick = now;
            // Meer dan 20 seconden niets gehoord: de pc sliep. Was de tijd intussen voorbij, dan niet alsnog
            // uitvoeren (je zou je pc anders net na het wakker worden zien afsluiten), maar stoppen en het melden.
            if (gap > 20 && now >= Target)
            {
                Active = false; timer.Stop();
                Log.Info("Pc kwam uit slaapstand na de geplande tijd: timer gestopt");
                Raise(Resumed);
                Raise(Changed);
                return;
            }
            if (now >= Target)
            {
                Active = false; timer.Stop();
                Raise(Changed);
                Raise(Due);
                return;
            }
            int warn = Settings.Current.WarnMinutes;
            if (!Warned && warn > 0 && (Target - now).TotalSeconds <= warn * 60 + 0.5)
            {
                Warned = true;
                Raise(Warn);
            }
            Raise(Tick);
        }

        void Raise(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        public void Dispose() { timer.Dispose(); }
    }
}
