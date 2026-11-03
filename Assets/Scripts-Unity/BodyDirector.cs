using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DnDCombat.Core;
using Grid = DnDCombat.Core.Grid;

namespace DnDCombat.Unity
{
    // Makes the bodies in the scene show what the Core just did. It's the one place that
    // moves, turns and animates bodies, so it doesn't matter who caused a move or an attack:
    // the player, the AI, or anything added later.
    public class BodyDirector : MonoBehaviour
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // The Core does things instantly. Bodies need time to show them. So every Core event
        // becomes a JOB in one queue, and the jobs play one after another:
        //   Moved          -> a walk job: run along the path, turning into each step
        //   AttackResolved -> an attack job: turn to the target, swing, the target flinches or dies
        // One queue for the whole board is enough, because only one creature acts at a time.
        // While anything is queued or playing, IsBusy is true. Other scripts wait for it:
        // clicks are ignored, the previews hide, and the turn driver doesn't end the turn early.
        //
        // Each job has two halves:
        //   Play    a coroutine that shows it over time (the real game)
        //   Finish  jumps straight to the end result (tests, and anything that needs to skip)
        //
        // Example: the Skeleton walks 3 squares, then attacks the Fighter and hits.
        //   walk job:    3 squares at 4 per second = 0.75 s, turning into each step
        //   attack job:  turn to face the Fighter (about 0.1 s), swing, 0.35 s later the Fighter
        //                flinches, 0.5 s to recover
        //   IsBusy is true for about 1.7 s, and the turn driver waits for all of it.

        [SerializeField] private GameManager _game;

        [Header("Moving")]
        [SerializeField] private float _moveSpeed = 4f;            // squares per second. Tune until the feet stop sliding.
        [SerializeField] private float _turnSpeed = 720f;          // degrees per second (a half turn in a quarter second)

        [Header("Attacking")]
        [SerializeField] private float _attackHitTime = 0.35f;     // seconds from the swing starting to it landing
        [SerializeField] private float _attackRecoverTime = 0.5f;  // seconds after it lands before the next job

        [Header("Testing")]
        [SerializeField] private bool _instant;                    // skip the show and jump straight to the end of every job

        private CombatController _subscribedTo;     // remembered so OnDestroy unsubscribes from the same one

        // One thing to show. Play does it over time, Finish jumps straight to the end.
        private class BodyJob
        {
            public IEnumerator Play;
            public Action Finish;
        }

        private readonly Queue<BodyJob> _jobs = new Queue<BodyJob>();
        private bool _running;      // true while RunJobs is working through the queue

        // True while anything is still being shown. Other scripts wait until this is false.
        public bool IsBusy => _running || _jobs.Count > 0;
        public event Action<AttackReport> AttackLanded;

        ///////////////////////////////////
        // ---------- Start ---------- //
        ///////////////////////////////////
        // Start, not Awake: GameManager builds everything in its Awake, so by now it all exists
        private void Start()
        {
            if (_game == null || _game.Controller == null)
            {
                Debug.LogWarning("BodyDirector: no ready GameManager, so bodies won't move or turn.");
                return;
            }

            _subscribedTo = _game.Controller;
            _subscribedTo.Moved += OnMoved;
            _subscribedTo.AttackResolved += OnAttackResolved;

            FaceEveryoneAtTheNearestEnemy();
        }

        private void OnDestroy()
        {
            if (_subscribedTo == null) return;

            _subscribedTo.Moved -= OnMoved;
            _subscribedTo.AttackResolved -= OnAttackResolved;
            _subscribedTo = null;
        }

        ////////////////////////////////////////////
        // ---------- Reacting to events ---------- //
        ////////////////////////////////////////////
        private void OnMoved(MoveReport report)
        {
            Enqueue(MakeWalkJob(report));
        }

        private void OnAttackResolved(AttackReport report)
        {
            Enqueue(MakeAttackJob(report));
        }

        ////////////////////////////////////////
        // ---------- The job queue ---------- //
        ////////////////////////////////////////
        private void Enqueue(BodyJob job)
        {
            if (job == null) return;
            if(_instant)
            {
                job.Finish();
                return;
            }
            else
            {
                _jobs.Enqueue(job);
                if (!_running)
                {
                    StartCoroutine(RunJobs());
                }
            }
        }

        private IEnumerator RunJobs()
        {
            _running = true;
            try
            {
                while (_jobs.Count > 0)
                {
                    BodyJob job = _jobs.Dequeue();
                    yield return StartCoroutine(job.Play);
                    job.Finish();
                }

            }
            finally
            {
                _running = false;
            }
        }

        ///////////////////////////////////
        // ---------- Walking ---------- //
        ///////////////////////////////////
        private BodyJob MakeWalkJob(MoveReport report)
        {
            CombatantView view = _game.ViewOf(report.Mover);
            if (view == null) return null;

            BodyJob job = new BodyJob();
            job.Play = Walk(view, report);

            // "() => { ... }" is a small method with no name, stored to run later.
            // Here: put the body on its last square, facing along the last step, standing still.
            job.Finish = () =>
            {
                view.SnapToGrid();
                view.FaceDirection(Step(report.LastStepFrom, report.To));
                view.SetMoveSpeed(0f);
            };
            return job;
        }

        private IEnumerator Walk(CombatantView view, MoveReport report)
        {
            view.SetMoveSpeed(_moveSpeed);
            GridPosition from = report.From;
            foreach(GridPosition to in report.Path)
            {
                Vector3 start = _game.Space.ToWorldFloorCentre(from);
                Vector3 end = _game.Space.ToWorldFloorCentre(to);
                float stepTime = 1f / _moveSpeed;   // seconds per square
                Quaternion facing = Quaternion.LookRotation(Step(from, to));
                for (float t = 0f; t < stepTime; t += Time.deltaTime)
                {
                    view.transform.position = Vector3.Lerp(start, end, t / stepTime);
                    view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation,
                                                  facing, _turnSpeed * Time.deltaTime);
                    yield return null;
                }
                view.transform.position = end;    // land exactly, whatever the last frame did
                from = to;
            }
            view.SetMoveSpeed(0f);
        }

        //////////////////////////////////
        // ---------- Attacking ---------- //
        //////////////////////////////////
        private BodyJob MakeAttackJob(AttackReport report)
        {
            CombatantView attacker = _game.ViewOf(report.Attacker);
            CombatantView target = _game.ViewOf(report.Target);
            if (attacker == null || target == null) return null;

            bool landed = false;
            Action land = () =>
            {
                if (landed) return;          // already announced: do nothing
                landed = true;
                AttackLanded?.Invoke(report);
            };

            BodyJob job = new BodyJob();
            job.Play = Attack(attacker, target, report, land);

            // The end result: facing the target, and the target lying down if that killed it
            job.Finish = () =>
            {
                attacker.FaceTowards(target.transform.position);
                land();   // announce it to the log and the HP bar
                if (report.TargetDefeated) target.SetDead(true);
            };
            return job;
        }

        private IEnumerator Attack(CombatantView attacker, CombatantView target, AttackReport report, Action land)
        {
            yield return TurnTowards(attacker, target.transform.position);

            attacker.PlayAttack();

            yield return new WaitForSeconds(_attackHitTime);   // the moment the blade lands

            land();   // announce it to the log and the HP bar

            if (!report.Result.Hit)
            {             
            }
            else if (report.TargetDefeated)
            {
                target.SetDead(true);
            }
            else
            {
                target.PlayHit();
            }
            yield return new WaitForSeconds(_attackRecoverTime);
        }

        //////////////////////////////////
        // ---------- Turning ---------- //
        //////////////////////////////////
        // Turns smoothly until it faces the point, then stops
        private IEnumerator TurnTowards(CombatantView view, Vector3 point)
        {
            Vector3 direction = point - view.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                yield break;   // standing on it: no direction, so no facing
            }
            Quaternion facing = Quaternion.LookRotation(direction);
            while (Quaternion.Angle(view.transform.rotation, facing) > 0.5f)
            {
                view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation,
                                                  facing, _turnSpeed * Time.deltaTime);
                yield return null;
            }
            view.transform.rotation = facing;
        }

        //////////////////////////////////
        // ---------- Helpers ---------- //
        //////////////////////////////////
        // The flat world direction of one step between two squares
        private Vector3 Step(GridPosition from, GridPosition to)
        {
            Vector3 step = _game.Space.ToWorldFloorCentre(to) - _game.Space.ToWorldFloorCentre(from);
            step.y = 0f;
            return step;
        }

        ///////////////////////////////////////////////
        // ---------- Facing at the start ---------- //
        ///////////////////////////////////////////////
        private void FaceEveryoneAtTheNearestEnemy()
        {
            foreach (Combatant combatant in _game.Combatants)
            {
                CombatantView view = _game.ViewOf(combatant);
                if (view == null) continue;

                view.FaceTowards(NearestEnemyPoint(combatant, view.transform.position));
            }
        }

        // Where the nearest living enemy stands, or the middle of the arena if there are none.
        // "Nearest" is straight-line distance, so between two enemies the same number of squares
        // away it picks the one more directly in front.
        private Vector3 NearestEnemyPoint(Combatant combatant, Vector3 from)
        {
            Vector3 best = ArenaCentre();
            float bestDistance = float.MaxValue;

            foreach (Combatant other in _game.Combatants)
            {
                if (other.Team == combatant.Team || other.Creature.IsDefeated) continue;

                CombatantView view = _game.ViewOf(other);
                if (view == null) continue;

                float distance = (view.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = view.transform.position;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // The middle of the arena's floor
        private Vector3 ArenaCentre()
        {
            Grid grid = _game.Controller.Grid;
            float size = _game.Space.CellSize;
            return _game.Space.Origin + new Vector3(grid.Width * size / 2f, 0f, grid.Depth * size / 2f);
        }
    }
}
