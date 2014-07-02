using Stateless;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace StatelessExample
{
    class Program
    {
        private enum Trigger
        {
            StartDevelopment,
            DevelopmentFinished,
            StartTest,
            TestPassed,
            TestFailed
        }

        private Dictionary<string, State> states = new Dictionary<string, State>();
        private CompositeDisposable disposables = new CompositeDisposable();
        private StateMachine<State, Trigger> jiraMachine;

        //Toggle this to see the effect of states with multiple next states
        private bool simulateTestPassing = false;

        public Program()
        {
            states.Add("ReadyForDevelopment", new State("ReadyForDevelopment", null, null));
            states.Add("InDevelopment", new State("InDevelopment",
                (x) => Console.WriteLine(string.Format("Entered InDevelopment {0} State", x.Name)),
                (x) => Console.WriteLine(string.Format("Exited InDevelopment {0} State", x.Name))));
            states.Add("ReadyForTest", new State("ReadyForTest", null, null));
            states.Add("InTest", new State("InTest", null, null));
            states.Add("Closed", new State("Closed", null, null));
        }


        public void Run()
        {
            jiraMachine = new StateMachine<State, Trigger>(states["ReadyForDevelopment"]);

            jiraMachine.Configure(states["ReadyForDevelopment"])
                .OnEntry(s => PrintStateOnEntry())
                .OnExit(s => PrintStateOnExit())
                .Permit(Trigger.StartDevelopment, states["InDevelopment"]);

            jiraMachine.Configure(states["InDevelopment"])
                .OnEntry(s =>
                {
                    disposables.Add(Observable.Interval(TimeSpan.FromSeconds(1))
                    .Subscribe(x => SendStyleCopNagEmail()));
                    jiraMachine.State.OnEntryStateAction(jiraMachine.State);
                })
                .OnExit(s =>
                {
                    disposables.Dispose();
                    jiraMachine.State.OnExitStateAction(jiraMachine.State);
                })
                .Permit(Trigger.DevelopmentFinished, states["ReadyForTest"]);

            jiraMachine.Configure(states["ReadyForTest"])
                .OnEntry(s => PrintStateOnEntry())
                .OnExit(s => PrintStateOnExit())
                .Permit(Trigger.StartTest, states["InTest"]);

            jiraMachine.Configure(states["InTest"])
                .OnEntry(s => PrintStateOnEntry())
                .OnExit(s => PrintStateOnExit())
                .Permit(Trigger.TestFailed, states["InDevelopment"])
                .Permit(Trigger.TestPassed, states["Closed"]);

            jiraMachine.Configure(states["Closed"])
                .OnEntry(s => PrintStateOnEntry())
                .OnExit(s => PrintStateOnExit());

            Fire(jiraMachine, Trigger.StartDevelopment);

            Action completeTheRemainingStates = () =>
            {
                Fire(jiraMachine, Trigger.DevelopmentFinished);
                Fire(jiraMachine, Trigger.StartTest);
                Fire(jiraMachine, simulateTestPassing ? Trigger.TestPassed : Trigger.TestFailed);
            };

            disposables.Add(Observable.Timer(TimeSpan.FromSeconds(5))
                .Subscribe(x => completeTheRemainingStates()));

            Console.ReadKey(true);
        }


        static void SendStyleCopNagEmail()
        {
            Console.WriteLine("Don't forget to use StyleCop settings for any JIRA checkin");
        }

        static void Fire(StateMachine<State, Trigger> jiraMachine, Trigger trigger)
        {
            Console.WriteLine("[Firing:] {0}", trigger);
            jiraMachine.Fire(trigger);
        }

        void PrintStateOnEntry()
        {
            Console.WriteLine(string.Format("Entered state : {0}", jiraMachine.State.Name));
        }

        void PrintStateOnExit()
        {
            Console.WriteLine(string.Format("Exited state : {0}", jiraMachine.State.Name));
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            p.Run();
        }
    }

    public class State
    {
        public string Name { get; private set; }

        /// <summary>
        /// Stateless has OnEntry/OnExit actions that can be run, but this just illustrates how you
        /// could go about creating your own states that run their own actions where good encapsulation is
        /// observed
        /// </summary>
        public Action<State> OnEntryStateAction { get; private set; }

        /// <summary>
        /// Stateless has OnEntry/OnExit actions that can be run, but this just illustrates how you
        /// could go about creating your own states that run their own actions where good encapsulation is
        /// observed
        /// </summary>
        public Action<State> OnExitStateAction { get; private set; }

        public State(string name, Action<State> onEntryStateAction, Action<State> onExitStateAction)
        {
            Name = name;
            OnEntryStateAction = onEntryStateAction;
            OnExitStateAction = onExitStateAction;
        }
    }
}
