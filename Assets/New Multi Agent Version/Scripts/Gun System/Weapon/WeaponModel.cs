using System;
using UnityEngine;


public class WeaponModel
{
    public int MaxAmmo { get; private set; }
    public int CurrentAmmo { get; private set; }

    public float FireRate { get; private set; }
    public float ReloadTime { get; private set; }

    public bool IsReloading { get; private set; }

    private float lastShootTime;

    public event Action<int> OnAmmoChanged;
    public event Action OnReloadStarted;
    public event Action OnReloadFinished;

    public event Action OnAmmoEmpty;
    public event Action OnAmmoLow;


    public WeaponModel(int maxAmmo, float fireRate, float reloadTime)
    {
        MaxAmmo = maxAmmo;
        CurrentAmmo = maxAmmo;
        FireRate = fireRate;
        ReloadTime = reloadTime;
    }

    public bool CanShoot()
    {
        if (IsReloading) return false;
        if (CurrentAmmo <= 0) return false;
        if (UnityEngine.Time.time < lastShootTime + FireRate) return false;

        return true;
    }

    public void ConsumeAmmo()
    {
        CurrentAmmo--;
        lastShootTime = UnityEngine.Time.time;

        OnAmmoChanged?.Invoke(CurrentAmmo);

        if (CurrentAmmo <= 0)
        {
            OnAmmoEmpty?.Invoke();
        }
        else if (CurrentAmmo <= MaxAmmo * 0.2f)
        {
            OnAmmoLow?.Invoke();
        }

        //Debug.Log("Ammo: " + CurrentAmmo + " / " + MaxAmmo);

    }


    public void StartReload()
    {
        if (IsReloading || CurrentAmmo == MaxAmmo)
            return;

        IsReloading = true;
        OnReloadStarted?.Invoke();
    }

    public void FinishReload()
    {
        CurrentAmmo = MaxAmmo;
        IsReloading = false;
        OnReloadFinished?.Invoke();
        OnAmmoChanged?.Invoke(CurrentAmmo);

        //Debug.Log("Reloaded → Ammo: " + CurrentAmmo);

    }

    public void ResetAmmo()
    {
        CurrentAmmo = MaxAmmo;
        IsReloading = false;
    }
    
}
