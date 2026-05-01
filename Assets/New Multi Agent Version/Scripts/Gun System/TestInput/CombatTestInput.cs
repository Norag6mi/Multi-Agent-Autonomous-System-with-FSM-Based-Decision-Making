using UnityEngine;

public class CombatTestInput : MonoBehaviour
{
    private CombatController combat;

    private void Start()
    {
        combat = GetComponent<CombatController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            combat.StartAttack();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            combat.StopAttack();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            combat.Reload();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            combat.StartHeal(30, 2f);
        }

    }
}
