using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class WorldDistanceVisualizer : MonoBehaviour
{
    [Header("Targets")]
    public List<Transform> targets = new List<Transform>();

    [Header("Line Settings")]
    public Material lineMaterial;
    public float lineWidth = 0.01f;

    [Header("Text Settings")]
    public TMP_FontAsset font;
    public float baseTextScale = 0.02f;
    public float scaleMultiplier = 1f;

    private class LineData
    {
        public Transform target;
        public LineRenderer line;
        public Transform textTransform;
        public TMP_Text text;
    }

    private readonly List<LineData> lines = new List<LineData>();
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;

        foreach (Transform target in targets)
        {
            if (target == null)
                continue;

            // Line Renderer
            GameObject lineObj = new GameObject("DistanceLine");
            lineObj.transform.SetParent(transform);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;

            // TMP Text
            GameObject textObj = new GameObject("DistanceText");
            textObj.transform.SetParent(transform);

            TMP_Text tmp = textObj.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 1f;
			tmp.textWrappingMode = TextWrappingModes.Normal;

			lines.Add(new LineData
            {
                target = target,
                line = lr,
                textTransform = textObj.transform,
                text = tmp
            });
        }
    }

    void Update()
    {
        if (mainCamera == null)
            return;

        Vector3 sourcePos = transform.position;

        foreach (LineData data in lines)
        {
            if (data.target == null)
                continue;

            Vector3 targetPos = data.target.position;

            // Update line
            data.line.SetPosition(0, sourcePos);
            data.line.SetPosition(1, targetPos);

            // Midpoint
            Vector3 midPoint = (sourcePos + targetPos) * 0.5f;
            data.textTransform.position = midPoint;

            // Distance calculation
            float distanceMeters = Vector3.Distance(sourcePos, targetPos);

            int meters = Mathf.FloorToInt(distanceMeters);
            float remaining = (distanceMeters - meters) * 100f;
            int centimeters = Mathf.FloorToInt(remaining);
            int millimeters = Mathf.RoundToInt((remaining - centimeters) * 10f);

            data.text.text = $"{meters} m, {centimeters} cm, {millimeters} mm";

            // Face camera
            data.textTransform.rotation = Quaternion.LookRotation(
                data.textTransform.position - mainCamera.transform.position);

            // Dynamic scale based on camera distance
            float camDist = Vector3.Distance(mainCamera.transform.position, midPoint);
            float scale = camDist * baseTextScale * scaleMultiplier;
            data.textTransform.localScale = Vector3.one * scale;
        }
    }
}
