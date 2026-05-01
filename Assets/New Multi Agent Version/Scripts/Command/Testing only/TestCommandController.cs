// Assets/Scripts/Command/TestCommandController.cs
// TEMPORARY — for testing command pipeline. Delete after verification.
using UnityEngine;
using System.Collections.Generic;

public class TestCommandController : MonoBehaviour
{
    private List<CommandReceiver> alphaReceivers = new List<CommandReceiver>();
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        Invoke(nameof(FindReceivers), 0.3f);
    }

    private void FindReceivers()
    {
        foreach (var member in FactionManager.Instance.GetFactionMembers(FactionType.Alpha))
        {
            CommandReceiver receiver = member.GetComponent<CommandReceiver>();
            if (receiver != null)
                alphaReceivers.Add(receiver);
        }
        Debug.Log($"[TEST] Found {alphaReceivers.Count} Alpha agents with CommandReceiver");
    }

    private void Update()
    {

        if (alphaReceivers.Count == 0) return; // Not ready yet

        // Left click = Move To
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                foreach (var receiver in alphaReceivers)
                    receiver.ReceiveMoveOrder(hit.point);

                CommandMarker.Spawn(hit.point, Color.blue);
                Debug.Log($"[TEST] Move To → {hit.point}");
            }
        }

        // Right click = Regroup to click position
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                foreach (var receiver in alphaReceivers)
                    receiver.ReceiveRegroup(hit.point);

                CommandMarker.Spawn(hit.point, Color.green);
                Debug.Log($"[TEST] Regroup → {hit.point}");
            }
        }

        // H = Hold
        if (Input.GetKeyDown(KeyCode.H))
        {
            foreach (var receiver in alphaReceivers)
                receiver.ReceiveHold();
            Debug.Log("[TEST] Hold Position");
        }

        // R = Release
        if (Input.GetKeyDown(KeyCode.R))
        {
            foreach (var receiver in alphaReceivers)
                receiver.ReceiveRelease();
            Debug.Log("[TEST] Released to autonomous");
        }
    }
}