using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Plays the narration and fires everything in the timeline at its time.
    // All timing comes from LessonClock (the audio position), never from frame
    // counting, so pacing cannot drift from the narration. The same schedule
    // runs in every condition; only PauseDirector behaves differently.
    // Runs first so Config is set before any other component's Awake reads it.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public class LessonController : MonoBehaviour
    {
        [SerializeField] TextAsset timelineJson;
        [Tooltip("The recorded narration. Leave empty to use the generated placeholder.")]
        [SerializeField] AudioClip narration;
        [SerializeField] QubitState state;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] AngleArcsView angleArcs;
        [SerializeField] VectorEffects effects;
        [SerializeField] CollapseFlashView flash;
        [SerializeField] MeasurementDemo demo;

        [Header("Development only")]
        [Tooltip("Used when no session has set the condition (before Checkpoint 6's launch screen).")]
        [SerializeField] Condition devCondition = Condition.PassiveVR;
        [SerializeField] bool startOnPlay = true;
        [Tooltip("Editor only: runs the lesson faster for dry runs. Up to 3x speeds up the narration (audio pitch); " +
                 "above 3x the narration is silent and lesson time runs off frame time. Builds always use 1.")]
        [SerializeField, Range(1f, 18f)] float devPlaybackSpeed = 1f;

        public event Action<LessonTimeline.Segment> SegmentStarted;
        public event Action<int, LessonTimeline.Segment, LessonTimeline.PauseTask, double> PauseStarted; // index, segment, task, onset
        public event Action<int> PauseEnded;
        public event Action<LessonTimeline.LessonEvent> MeasurementDemoRequested;
        public event Action LessonEnded;

        public LessonTimeline Timeline { get; private set; }
        public ConditionConfig Config { get; private set; }
        public double Time => clock != null ? clock.Time : 0;
        public bool IsRunning => running;

        AudioSource source;
        LessonClock clock;
        readonly List<ScheduledItem> schedule = new List<ScheduledItem>();
        int nextItem;
        bool running;
        float realStartTime;
        readonly StringBuilder report = new StringBuilder();
        double maxLateness;

        struct ScheduledItem
        {
            public double Time;
            public int Order; // ties: pause offset, then segment start, then pause onset, then events
            public string Label;
            public Action Fire;
        }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // narration is not positional
            // Resolved here (not in Begin) so other components can read it in their Start.
            Config = ConditionConfig.Current ?? new ConditionConfig(devCondition);
        }

        void Start()
        {
            if (startOnPlay) Begin();
        }

        public void Begin()
        {
            Timeline = LessonTimeline.FromJson(timelineJson.text);

            List<string> errors = Timeline.Validate();
            CheckPauseStartStates(errors);
            if (narration == null)
            {
                narration = PlaceholderNarration.Create(Timeline);
                Debug.LogWarning("[Lesson] No narration clip assigned: using the generated placeholder.");
            }
            double demoWindow = Timeline.pauseSchedule.measurementDemoEnd - Timeline.pauseSchedule.measurementDemoStart;
            if (MeasurementDemo.Duration > demoWindow)
                errors.Add($"measurement demo takes {MeasurementDemo.Duration}s but its window is {demoWindow}s");
            if (Math.Abs(narration.length - Timeline.lessonDuration) > 0.5)
                errors.Add($"narration clip is {narration.length:F2}s but the timeline lasts {Timeline.lessonDuration:F2}s");
            if (errors.Count > 0)
            {
                foreach (string error in errors) Debug.LogError("[Lesson] Timeline error: " + error);
                Debug.LogError("[Lesson] Not starting: fix lesson_timeline.json.");
                return;
            }

            BuildSchedule();
            double frameClockSpeed = 0;
#if UNITY_EDITOR
            if (devPlaybackSpeed > 3f) frameClockSpeed = devPlaybackSpeed;
#endif
            clock = new LessonClock(source, frameClockSpeed);
            animator.Clock = () => clock.Time;
            effects.Clock = () => clock.Time;
            flash.Clock = () => clock.Time;
            source.clip = narration;
            source.pitch = 1f;
#if UNITY_EDITOR
            source.pitch = Mathf.Min(devPlaybackSpeed, 3f);
            // Fades and other frame-time effects keep up with the faster lesson.
            UnityEngine.Time.timeScale = devPlaybackSpeed;
            if (clock.IsFrameClock) source.mute = true;
            if (devPlaybackSpeed != 1f) Debug.LogWarning($"[Lesson] Dev playback speed {devPlaybackSpeed}x (Editor only" +
                                                         (clock.IsFrameClock ? ", silent frame-time clock)." : ")."));
#endif
            Debug.Log($"[Lesson] {Config} ready. {Timeline.segments.Length} segments, {Timeline.segments.Length} pauses x " +
                      $"{Timeline.pauseSchedule.pauseLength}s, {Timeline.events.Length} events, duration {Timeline.lessonDuration}s, " +
                      $"clip '{narration.name}' {narration.length:F3}s");
            StartCoroutine(StartWhenSceneSettles());
        }

        // The first frames after a scene loads can hitch for seconds (shader compilation,
        // asset loading). Narration started during a hitch would keep playing while
        // nothing on screen moves, so the first events would fire late. Wait for a run
        // of normal frames first. This happens before lesson time 0, so it never changes
        // any lesson timing.
        IEnumerator StartWhenSceneSettles()
        {
            const int StableFramesNeeded = 20;
            const float HitchSeconds = 0.1f;
            const float MaxWaitSeconds = 10f;
            float waitStart = UnityEngine.Time.realtimeSinceStartup;
            int stableFrames = 0;
            int frames = 0;
            while (stableFrames < StableFramesNeeded && UnityEngine.Time.realtimeSinceStartup - waitStart < MaxWaitSeconds)
            {
                yield return null;
                frames++;
                stableFrames = UnityEngine.Time.unscaledDeltaTime < HitchSeconds ? stableFrames + 1 : 0;
            }
            Debug.Log($"[Lesson] {Config} narration starts after {frames} warm-up frames " +
                      $"({UnityEngine.Time.realtimeSinceStartup - waitStart:F2}s, settled={stableFrames >= StableFramesNeeded})");
            running = true;
            realStartTime = UnityEngine.Time.realtimeSinceStartup;
            source.Play();
        }

        void Update()
        {
            if (!running) return;
            clock.Tick(UnityEngine.Time.unscaledDeltaTime, false);
            while (nextItem < schedule.Count && schedule[nextItem].Time <= clock.Time)
            {
                ScheduledItem item = schedule[nextItem++];
                FinishTransitionsEndingBy(item.Time);
                Record(item.Label, item.Time);
                item.Fire();
            }
        }

#if UNITY_EDITOR
        // Editor only: jump the narration forward to take screenshots of late moments.
        // Everything scheduled before the new time fires in order, as after a hitch.
        public void DevSeek(double seconds)
        {
            source.time = (float)seconds;
            clock.DevSeek(seconds);
            Debug.LogWarning($"[Lesson] Dev seek to {seconds:F1}s (Editor only).");
        }
#endif

        // Writes one line to the pacing report: when something was scheduled and when it actually fired.
        public void Record(string label, double scheduled)
        {
            double lateness = clock.Time - scheduled;
            maxLateness = Math.Max(maxLateness, lateness);
            report.AppendLine($"[Pacing] {Config} {label,-28} scheduled={LessonTimeline.Seconds(scheduled)} " +
                              $"fired={LessonTimeline.Seconds(clock.Time)} audio={LessonTimeline.Seconds(clock.AudioTime)} " +
                              $"late={lateness * 1000:F1}ms");
        }

        void BuildSchedule()
        {
            schedule.Clear();
            nextItem = 0;
            LessonTimeline t = Timeline;
            for (int i = 0; i < t.segments.Length; i++)
            {
                int index = i;
                LessonTimeline.Segment s = t.segments[i];
                LessonTimeline.PauseTask task = t.Task(s.task);
                Add(s.start, 1, $"segment {s.id} start", () => SegmentStarted?.Invoke(s));
                Add(s.pauseStart, 2, $"pause{index + 1} onset", () =>
                {
                    CheckLiveStartState(task);
                    PauseStarted?.Invoke(index, s, task, s.pauseStart);
                });
                Add(s.pauseEnd, 0, $"pause{index + 1} offset", () => PauseEnded?.Invoke(index));
            }
            foreach (LessonTimeline.LessonEvent e in t.events) Add(e.t, 3, "event " + e.type + " " + e.state + e.gate + e.target, () => Fire(e));
            Add(t.lessonDuration, 4, "lesson end", EndLesson);
            schedule.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
        }

        // When several items come due in one frame (a hitch), an arc or re-preparation
        // that should already have ended must land before the next item starts from its
        // result. The timeline rules guarantee these never overlap, so this is exact.
        void FinishTransitionsEndingBy(double time)
        {
            if (animator.IsPlaying && animator.ScheduledEnd <= time) animator.Complete();
            if (effects.IsBusy && effects.ScheduledEnd <= time) effects.Complete();
        }

        void Add(double time, int order, string label, Action fire) =>
            schedule.Add(new ScheduledItem { Time = time, Order = order, Label = label, Fire = fire });

        void Fire(LessonTimeline.LessonEvent e)
        {
            switch (e.type)
            {
                case "set_state":
                    effects.Reprepare(Qubit.FromName(e.state), e.t, Timeline.reprepareDuration);
                    break;
                case "apply_gate":
                    // Scheduled start, so a late frame shortens nothing and delays nothing.
                    animator.Play(Gate.FromName(e.gate), e.duration, e.t);
                    break;
                case "show":
                case "hide":
                    if (e.target == "angle_arcs") angleArcs.Visible = e.type == "show";
                    if (e.target == "measurement_panel" && e.type == "hide") demo.HidePanel();
                    break;
                case "measure_demo":
                    MeasurementDemoRequested?.Invoke(e);
                    break;
            }
        }

        // The timeline says what state each pause starts from; check the live sphere agrees.
        void CheckLiveStartState(LessonTimeline.PauseTask task)
        {
            if (animator.IsPlaying) animator.Complete();
            effects.Complete();
            demo.Finish();
            Qubit expected = Qubit.FromName(task.startState);
            if (!state.Current.IsSameStateAs(expected, 1e-6))
                Debug.LogError($"[Lesson] {task.id}: live state {state.Current.Bloch} does not match timeline start state |{task.startState}>");
        }

        // The same check before starting, by replaying the timeline without animation.
        void CheckPauseStartStates(List<string> errors)
        {
            if (errors.Count > 0) return; // replay needs a structurally valid timeline
            List<Qubit> states = Timeline.StatesAtPauseOnsets();
            for (int i = 0; i < states.Count; i++)
            {
                LessonTimeline.PauseTask task = Timeline.Task(Timeline.segments[i].task);
                if (!states[i].IsSameStateAs(Qubit.FromName(task.startState), 1e-6))
                    errors.Add($"{task.id}: scripted events reach {states[i].Bloch} at pause onset, task expects |{task.startState}>");
            }
        }

        float devSpeedForReport => UnityEngine.Time.timeScale;

        void EndLesson()
        {
            running = false;
            float realSeconds = UnityEngine.Time.realtimeSinceStartup - realStartTime;
            var pauses = new StringBuilder();
            foreach (LessonTimeline.Segment s in Timeline.segments)
                pauses.Append($"[{LessonTimeline.Seconds(s.pauseStart)}-{LessonTimeline.Seconds(s.pauseEnd)}] ");
            Debug.Log(report.ToString());
            Debug.Log($"[Pacing] {Config} SUMMARY lessonTime={LessonTimeline.Seconds(clock.Time)} " +
                      $"scheduledTotal={LessonTimeline.Seconds(Timeline.lessonDuration)} pauses={pauses}" +
                      $"maxLateness={maxLateness * 1000:F1}ms real={realSeconds:F1}s at {devSpeedForReport}x");
            LessonEnded?.Invoke();
        }
    }
}
