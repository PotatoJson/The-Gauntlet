using UnityEngine;

public class Billboard : MonoBehaviour
{
    public enum BillboardMode { Camera, Player }

    [Header("Settings")]
    public BillboardMode mode = BillboardMode.Camera;
    public bool lockYAxis = true;
    public bool flipForward = false;

    [Header("Optional Target")]
    [Tooltip("If mode is Player and this is null, it will search for the 'Player' tag.")]
    public Transform target;

    private Transform _camTransform;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (Camera.main != null) _camTransform = Camera.main.transform;
        
        if (mode == BillboardMode.Player && target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (mode == BillboardMode.Camera)
        {
            if (_camTransform == null)
            {
                if (Camera.main != null) _camTransform = Camera.main.transform;
                else return;
            }

            Vector3 lookDirection = flipForward ? -_camTransform.forward : _camTransform.forward;
            if (lockYAxis) lookDirection.y = 0;
            
            transform.LookAt(transform.position + lookDirection);
        }
        else if (mode == BillboardMode.Player)
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
                else return;
            }

            Vector3 targetPosition = target.position;
            if (lockYAxis) targetPosition.y = transform.position.y;

            if (flipForward)
            {
                Vector3 directionAway = transform.position - targetPosition;
                transform.LookAt(transform.position + directionAway);
            }
            else
            {
                transform.LookAt(targetPosition);
            }
        }
    }
}


