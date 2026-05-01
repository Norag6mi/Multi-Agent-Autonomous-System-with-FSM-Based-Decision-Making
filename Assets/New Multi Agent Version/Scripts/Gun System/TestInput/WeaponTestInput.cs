using UnityEngine;

public class WeaponTestInput : MonoBehaviour
{
    private WeaponComponent weapon;

    private void Start()
    {
        weapon = GetComponent<WeaponComponent>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            weapon.TryStartFiring();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            weapon.StopFiring();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            weapon.TryReload();
        }
    }

}
