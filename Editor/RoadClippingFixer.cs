using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal readonly struct RoadClippingFixReport
    {
        public RoadClippingFixReport(RoadBuildReport build, int adjustedKnots, int smoothedKnots,
            bool remainingTightCorner = false, float beforeScore = 0f, float afterScore = 0f)
        {
            Build = build;
            AdjustedKnots = adjustedKnots;
            SmoothedKnots = smoothedKnots;
            RemainingTightCorner = remainingTightCorner;
            BeforeScore = beforeScore;
            AfterScore = afterScore;
        }

        public RoadBuildReport Build { get; }
        public int AdjustedKnots { get; }
        public int RemovedKnots => 0;
        public int SmoothedKnots { get; }
        public bool RemainingTightCorner { get; }
        public float BeforeScore { get; }
        public float AfterScore { get; }
        public bool Succeeded => Build.Succeeded && !RemainingTightCorner && Build.NarrowedSamples == 0;
    }

    internal static class RoadClippingFixer
    {
        public static RoadClippingFixReport Fix(SplineRoad road)
        {
            if (road == null)
                return new RoadClippingFixReport(new RoadBuildReport(false, "Select a road first."), 0, 0);
            if (road.IsBaked || road.Profile == null)
                return new RoadClippingFixReport(road.Rebuild(), 0, 0);

            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null)
                return new RoadClippingFixReport(road.Rebuild(), 0, 0);

            int adjustedKnots = 0;
            int smoothedKnots = 0;
            bool recordedUndo = false;
            bool unresolved = false;
            float beforeScore = 0f;
            float afterScore = 0f;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Smooth Road Clipping");

            foreach (Spline spline in container.Splines)
            {
                if (spline == null || spline.Count < 3) continue;
                RoadSplineRepairResult repair = RoadSplineRepairUtility.Repair(spline, container.transform, road.Profile.Width);
                unresolved |= repair.Unresolved;
                beforeScore += repair.BeforeScore;
                afterScore += repair.AfterScore;
                if (repair.AdjustedKnots == 0) continue;

                if (!recordedUndo)
                {
                    Undo.RegisterCompleteObjectUndo(container, "Smooth Road Clipping");
                    recordedUndo = true;
                }
                ApplyRepair(spline, repair.Spline);
                adjustedKnots += repair.AdjustedKnots;
                smoothedKnots += repair.SmoothedKnots;
            }

            if (recordedUndo) EditorUtility.SetDirty(container);
            RoadBuildReport build = road.Rebuild();
            if (recordedUndo) road.RequestRebuild();
            if (recordedUndo || build.Succeeded)
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            return new RoadClippingFixReport(build, adjustedKnots, smoothedKnots, unresolved, beforeScore, afterScore);
        }

        private static void ApplyRepair(Spline spline, Spline repaired)
        {
            // Keep the Spline and knot indices so links and selections survive. Populate all
            // positions before recalculating tangents, then send one live-update notification.
            for (int i = 0; i < spline.Count; i++)
            {
                spline.SetTangentModeNoNotify(i, TangentMode.Broken);
                spline.SetKnotNoNotify(i, repaired[i]);
            }
            for (int i = 0; i < spline.Count; i++)
            {
                spline.SetAutoSmoothTensionNoNotify(i, repaired.GetAutoSmoothTension(i));
                spline.SetTangentModeNoNotify(i, repaired.GetTangentMode(i));
            }
            spline.SetKnot(spline.Count - 1, spline[spline.Count - 1]);
        }
    }
}
