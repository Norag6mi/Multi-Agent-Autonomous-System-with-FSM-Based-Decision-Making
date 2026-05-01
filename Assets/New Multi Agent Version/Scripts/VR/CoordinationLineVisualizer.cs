// Assets/Scripts/VR/CoordinationLineVisualizer.cs
using UnityEngine;
using System.Collections.Generic;

public class CoordinationLineVisualizer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private Color lineColor = new Color(0f, 1f, 1f, 0.6f);
    [SerializeField] private int poolSize = 10;

    private List<LineRenderer> linePool = new List<LineRenderer>();
    private List<float> lineTimers = new List<float>();
    private bool visible = true;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject lineObj = new GameObject("CoordLine");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.useWorldSpace = true;
            lr.enabled = false;

            linePool.Add(lr);
            lineTimers.Add(0f);
        }

        AgentCoordinator.OnThreatBroadcast += HandleBroadcast;
    }

    private void OnDestroy()
    {
        if (AgentCoordinator.Instance != null)
            AgentCoordinator.OnThreatBroadcast -= HandleBroadcast;
    }

    private void HandleBroadcast(AgentIdentity broadcaster, Vector3 threatPos, List<AgentIdentity> receivers)
    {
        if (!visible) return;

        for (int i = 0; i < receivers.Count && i < poolSize; i++)
        {
            if (!linePool[i].enabled)
            {
                linePool[i].SetPosition(0, broadcaster.transform.position);
                linePool[i].SetPosition(1, receivers[i].transform.position);
                linePool[i].enabled = true;
                lineTimers[i] = fadeDuration;
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (linePool[i].enabled)
            {
                lineTimers[i] -= Time.deltaTime;

                if (lineTimers[i] <= 0f)
                {
                    linePool[i].enabled = false;
                }
                else
                {
                    float alpha = lineTimers[i] / fadeDuration;
                    Color c = lineColor;
                    c.a = alpha * 0.6f;
                    linePool[i].startColor = c;
                    linePool[i].endColor = c;
                }
            }
        }
    }

    public void SetVisible(bool v)
    {
        visible = v;
    }
}