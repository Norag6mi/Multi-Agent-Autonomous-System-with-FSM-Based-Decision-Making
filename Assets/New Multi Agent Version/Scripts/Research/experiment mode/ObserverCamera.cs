using UnityEngine;

public class ObserverCamera : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float rotateSpeed = 60f;

    [Header("Limits")]
    [SerializeField] private float minHeight = 10f;
    [SerializeField] private float maxHeight = 50f;

    private void Update()
    {
        // WASD pan
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v) * panSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);

        // Scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 pos = transform.position;
        pos.y -= scroll * zoomSpeed;
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;

        // Q/E rotate
        if (Input.GetKey(KeyCode.Q))
            transform.RotateAround(transform.position, Vector3.up, -rotateSpeed * Time.deltaTime);
        if (Input.GetKey(KeyCode.E))
            transform.RotateAround(transform.position, Vector3.up, rotateSpeed * Time.deltaTime);
    }
}