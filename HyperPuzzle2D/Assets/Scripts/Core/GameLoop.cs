using System;

namespace HyperPuzzle2D.Core
{
    public enum RunMode
    {
        Endless,
        Daily
    }

    public enum GameState
    {
        Ready,
        Playing,
        Resolve,
        Failed,
        Cleared
    }

    /// <summary>
    /// Lightweight run state machine for MVP (cannon smash loop).
    /// </summary>
    public sealed class GameLoop
    {
        public GameState State { get; private set; } = GameState.Ready;
        public RunMode Mode { get; private set; } = RunMode.Endless;
        public int Score { get; private set; }
        public int Ammo { get; private set; }
        public int Combo { get; private set; }
        public int TargetsRemaining { get; private set; }
        public bool ReviveUsed { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> ScoreChanged;
        public event Action<int> AmmoChanged;
        public event Action<int> ComboChanged;

        public void StartRun(RunMode mode, int ammo, int targetCount)
        {
            Mode = mode;
            Score = 0;
            Combo = 0;
            Ammo = ammo;
            TargetsRemaining = targetCount;
            ReviveUsed = false;
            SetState(GameState.Playing);
            ScoreChanged?.Invoke(Score);
            AmmoChanged?.Invoke(Ammo);
            ComboChanged?.Invoke(Combo);
        }

        public bool TryConsumeAmmo()
        {
            if (State != GameState.Playing || Ammo <= 0)
            {
                return false;
            }

            Ammo--;
            AmmoChanged?.Invoke(Ammo);
            return true;
        }

        public void RegisterHit(int points, bool chain)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            Combo = chain ? Combo + 1 : 1;
            var multiplier = Math.Max(1, Combo);
            Score += points * multiplier;
            ScoreChanged?.Invoke(Score);
            ComboChanged?.Invoke(Combo);
        }

        public void RegisterTargetCleared()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            TargetsRemaining = Math.Max(0, TargetsRemaining - 1);
            if (TargetsRemaining == 0)
            {
                SetState(GameState.Cleared);
            }
        }

        public void NotifyShotResolved()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (TargetsRemaining <= 0)
            {
                SetState(GameState.Cleared);
                return;
            }

            if (Ammo <= 0)
            {
                SetState(GameState.Failed);
            }
        }

        public bool TryRevive(int bonusAmmo)
        {
            if (State != GameState.Failed || ReviveUsed)
            {
                return false;
            }

            ReviveUsed = true;
            Ammo = Math.Max(1, bonusAmmo);
            AmmoChanged?.Invoke(Ammo);
            SetState(GameState.Playing);
            return true;
        }

        public void ResetCombo()
        {
            Combo = 0;
            ComboChanged?.Invoke(Combo);
        }

        void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
