using System;

namespace HyperPuzzle2D.Core
{
    public enum ScoreReason
    {
        Broken,
        AmmoBonus,
        Explosion,
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

    public enum GameState
    {
        Ready,
        Playing,
        Failed,
        Cleared,
    }

    /// <summary>Stage run state machine for the paper-target loop.</summary>
    public sealed class GameLoop
    {
        public GameState State { get; private set; } = GameState.Ready;
        public int Score { get; private set; }
        public int Ammo { get; private set; }
        public int Combo { get; private set; }
        public int TargetsRemaining { get; private set; }
        public int ShotScore { get; private set; }
        public int ShotDestroyed { get; private set; }

        /// <summary>Points that pass the stage. Zero means "clear every target instead".</summary>
        public int TargetScore { get; private set; }

        public bool RequiresClearAll => TargetScore <= 0;

        public bool GoalMet => RequiresClearAll
            ? TargetsRemaining <= 0
            : TargetScore > 0 && Score >= TargetScore;

        public event Action<GameState> StateChanged;
        public event Action<int> ScoreChanged;
        public event Action<int> AmmoChanged;
        public event Action<ScoreAward> ScoreAwarded;

        public void StartRun(int ammo, int targetCount, int targetScore)
        {
            Score = 0;
            Combo = 0;
            Ammo = ammo;
            TargetsRemaining = targetCount;
            TargetScore = targetScore;
            ShotScore = 0;
            ShotDestroyed = 0;
            SetState(GameState.Playing);
            ScoreChanged?.Invoke(Score);
            AmmoChanged?.Invoke(Ammo);
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
            AmmoChanged?.Invoke(Ammo);
            return true;
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
        }

        public void NotifyShotResolved()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            // The run plays out rather than ending the instant the goal is crossed: a player gets to
            // spend the whole clip dismantling the figure, and the score is the sum of every part
            // hit. It only ends early when there is nothing left to shoot, which pays the leftover
            // ammo as a precision bonus.
            var clearedEverything = TargetsRemaining <= 0;
            if (clearedEverything)
            {
                var bonus = Ammo * 20;
                if (bonus > 0)
                {
                    Award(bonus, ScoreReason.AmmoBonus, false);
                }
            }

            if (!clearedEverything && Ammo > 0)
            {
                return;
            }

            SetState(GoalMet ? GameState.Cleared : GameState.Failed);
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
