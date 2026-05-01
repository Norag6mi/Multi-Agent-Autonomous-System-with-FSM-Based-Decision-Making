using UnityEngine;

public class CharacterAnimatorBridge : MonoBehaviour
{
    private Animator animator;

    //For single shoot
    //private static readonly int ShootTrigger = Animator.StringToHash("Shoot"); 

    private static readonly int IsFiring = Animator.StringToHash("IsFiring");
    private static readonly int ReloadTrigger = Animator.StringToHash("Reload");
    private static readonly int DieTrigger = Animator.StringToHash("Die");

    private static readonly int HealTrigger = Animator.StringToHash("Heal");




    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void SetFiring(bool value)
    {
        animator.SetBool(IsFiring, value);
    }

    // public void PlayShoot()
    // {
    //     animator.SetTrigger(ShootTrigger);
    // }

    public void PlayReload()
    {
        animator.SetTrigger(ReloadTrigger);
    }

    public void PlayHeal()
    {
        animator.SetTrigger(HealTrigger);
    }

    public void PlayDeath()
    {
        animator.SetTrigger(DieTrigger);
    }
}
