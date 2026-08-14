using System;

namespace HyperPuzzle2D.Core
{
    public enum ScoreReason
    {
        DirectHit,
        Broken,
        KnockedOff,
        Explosion,
        AmmoBonus,
    }

    public readonly struct ScoreAward
    {
        public readonly int Points;
        public readonly int ShotTotal;
        public readonly int Chain;
        public readonly ScoreReason Reason;

        public ScoreAward(int points, int shotTotal, int chain, ScoreReason reason)
        {
            Points = points;
            ShotTotal = shotTotal;
            Chain = chain;
            Reason = reason;
        }
    }

    public enum RunMode
    {
        Endless,
        Daily,
        Stage,
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
        public int ShotScore { get; private set; }
        public int ShotDestroyed { get; private set; }

        /// <summary>Points that pass the stage. Zero means "clear every target instead".</summary>
        public int TargetScore { get; private set; }

        public bool GoalMet => TargetScore > 0 && Score >= TargetScore;
        public bool ReviveUsed { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> ScoreChanged;
        public event Action<int> AmmoChanged;
        public event Action<int> ComboChanged;
        public event Action<int> TargetsChanged;
        public event Action<ScoreAward> ScoreAwarded;

        public void StartRun(RunMode mode, int ammo, int targetCount, int targetScore)
        {
            Mode = mode;
            Score = 0;
            Combo = 0;
            Ammo = ammo;
            TargetsRemaining = targetCount;
            TargetScore = targetScore;
            ShotScore = 0;
            ShotDestroyed = 0;
            ReviveUsed = false;
            SetState(GameState.Playing);
            ScoreChanged?.Invoke(Score);
            AmmoChanged?.Invoke(Ammo);
            TargetsChanged?.Invoke(TargetsRemaining);
            ComboChanged?.Invoke(Combo);
        }

        public bool TryConsumeAmmo()
        {
            if (State != GameState.Playing || Ammo <= 0)
            {
                return false;
            }

            Ammo--;
            ShotScore = 0;
            ShotDestroyed = 0;
            Combo = 0;
            ComboChanged?.Invoke(Combo);
            AmmoChanged?.Invoke(Ammo);
            return true;
        }

        public void RegisterDirectHit(int points)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            Award(Math.Max(0, points), ScoreReason.DirectHit, false);
        }

        public void RegisterDestruction(int points, ScoreReason reason)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            ShotDestroyed++;
            Combo = ShotDestroyed;
            var multiplier = Math.Max(1, Combo);
            Award(Math.Max(0, points) * multiplier, reason, true);
        }

        public void RegisterTargetCleared()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            TargetsRemaining = Math.Max(0, TargetsRemaining - 1);
            TargetsChanged?.Invoke(TargetsRemaining);
        }

        public void NotifyShotResolved()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (GoalMet)
            {
                var bonus = Ammo * 20;
                if (bonus > 0)
                {
                    Award(bonus, ScoreReason.AmmoBonus, false);
                }

                SetState(GameState.Cleared);
            }
            else if (Ammo <= 0)
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

        void Award(int points, ScoreReason reason, bool chain)
        {
            if (points <= 0)
            {
                return;
            }

            Score += points;
            ShotScore += points;
            ScoreChanged?.Invoke(Score);
            if (chain)
            {
                ComboChanged?.Invoke(Combo);
            }

            ScoreAwarded?.Invoke(new ScoreAward(points, ShotScore, Combo, reason));
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
