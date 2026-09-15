using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace _Scripts.Vicon
{
    /// <summary>
    /// A single authored calibration target pose. The pose is the world-space
    /// pose of the ghost's WristAnchor (the point exactly on the Hand/wrist bone
    /// of the ghost's rest pose), authored in scene coordinates.
    /// </summary>
    [Serializable]
    public class CalibrationTarget
    {
        [Tooltip("Display name of the target.")]
        public string name;

        [Tooltip("Canonical hand pose variant cycled across the position spread (e.g. palm-down flat, phone-hold vertical).")]
        public string poseVariant;

        [Tooltip("World-space position of the ghost WristAnchor for this target.")]
        public Vector3 position;

        [Tooltip("World-space rotation of the ghost WristAnchor for this target. Used only to orient the ghost; the solve uses the position.")]
        public Quaternion rotation;

        [Tooltip("When true, this target is excluded from the solve and used only as the verification target (one extra pose the corrected hand is checked against).")]
        public bool verificationOnly;
    }

    /// <summary>
    /// Serializable container for the list of calibration targets, used for
    /// JSON (de)serialization with JsonUtility.
    /// </summary>
    [Serializable]
    public class CalibrationTargetList
    {
        public List<CalibrationTarget> targets = new List<CalibrationTarget>();

        public static CalibrationTargetList FromJson(string json)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(json), "CalibrationTargetList: empty JSON string.");
            CalibrationTargetList list = JsonUtility.FromJson<CalibrationTargetList>(json);
            Assert.IsNotNull(list, "CalibrationTargetList: failed to parse JSON.");
            return list;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}
