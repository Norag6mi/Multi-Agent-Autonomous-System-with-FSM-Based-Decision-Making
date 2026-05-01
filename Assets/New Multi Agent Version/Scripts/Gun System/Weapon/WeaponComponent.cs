using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterAnimatorBridge))]
public class WeaponComponent : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private float reloadTime = 2f;

    [Header("Gun System")]
    [SerializeField] private GunScriptableObject gun;
    [SerializeField] private Transform muzzlePoint;

    private bool isFiring;

    public WeaponModel Model { get; private set; }

    private CharacterAnimatorBridge animatorBridge;

    private void Awake()
    {
        animatorBridge = GetComponent<CharacterAnimatorBridge>();

        Model = new WeaponModel(magazineSize, fireRate, reloadTime);
    }

    private void Start()
    {
        if (gun != null)
        {
            gun = Instantiate(gun);
            gun.Initialize(muzzlePoint, this);

        }
    }

    // public void TryShoot()
    // {
    //     if (!Model.CanShoot()) return;

    //     animatorBridge.PlayShoot();
    // }


    //Autoshoot

        public void TryStartFiring()
    {
        if (!Model.CanShoot()) return;

        isFiring = true;
        animatorBridge.SetFiring(true);
    }

    public void StopFiring()
    {
        isFiring = false;
        animatorBridge.SetFiring(false);
    }



    public void Fire()
    {
        if (!isFiring) return;
        if (!Model.CanShoot()) return;

        Model.ConsumeAmmo();

        AgentIdentity identity = GetComponent<AgentIdentity>();
        if (MetricsLogger.Instance != null && identity != null)
            MetricsLogger.Instance.RecordShotFired(identity.agentName);

        Vector3 direction = muzzlePoint.forward;
        gun.Shoot(direction);
    }





    public void TryReload()
    {
        if (Model.IsReloading) return;

        Model.StartReload();
        animatorBridge.PlayReload();
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        yield return new WaitForSeconds(reloadTime);
        Model.FinishReload();
    }

    public void ResetWeapon()
    {
        StopAllCoroutines();
        isFiring = false;

        if (Model != null)
            Model.ResetAmmo();

        if (animatorBridge != null)
            animatorBridge.SetFiring(false);
    }

}
