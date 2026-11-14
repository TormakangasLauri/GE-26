using UnityEngine;

public class Gun : MonoBehaviour
{
    public Camera cam;
    public Transform muzzle;
    public Transform gunModel;     // i am bad at coding and this variable is only used if the gun is flipped please don't ask
    public float range = 50f;
    public float damage = 1f;
    public float fireRate = 8f;
    float nextFire;

    [SerializeField] GameObject muzzleFlash;
    [SerializeField] LineRenderer tracer;
    [SerializeField] float effectDuration = 0.04f;
    [SerializeField] ParticleSystem muzzleSparks;
    [SerializeField] ParticleSystem impactSparks;

    [SerializeField] Transform gunVisual;
    [SerializeField] float recoilDistance = 0.1f;
    [SerializeField] float recoilRecovery = 20f;

    [SerializeField] float tracerWidth = 0.03f;
    [SerializeField] float tracerFadeTime = 0.1f;
    [SerializeField] Color tracerColor = new Color(1f, 0.9f, 0.4f, 1f);
    float tracerTimer;

    Vector3 gunRestPos;
    float hideEffectsTime;

    void Start() {
        gunRestPos = gunVisual.localPosition;
        if (tracer)
        {
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.enabled = false;
        }
        if (muzzleFlash) muzzleFlash.SetActive(false);
    }

    void Update()
    {
        // effects
        gunVisual.localPosition = Vector3.Lerp(gunVisual.localPosition, gunRestPos, recoilRecovery * Time.deltaTime);

        if (Time.time >= hideEffectsTime)
        {
            if (tracer && tracer.enabled)
            {
                tracerTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(tracerTimer / tracerFadeTime);

                Color start = tracerColor; start.a = t;
                Color end = tracerColor;   end.a = t * 0.2f;  // tail fades faster
                tracer.startColor = start;
                tracer.endColor = end;
                tracer.widthMultiplier = tracerWidth * t;

                if (t <= 0f) tracer.enabled = false;
            }

            if (muzzleFlash) muzzleFlash.SetActive(false);
        }

        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
        Ray mouseRay = cam.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(mouseRay, out float dist)) return;

        Vector3 aimDir = mouseRay.GetPoint(dist) - transform.position;
        aimDir.z = 0;
        aimDir.Normalize();

        // rotate the pivot of the gun towards cursor
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // this keeps the gun upright when it's turned to the left
        if (gunModel)
        {
            Vector3 s = gunModel.localScale;
            s.y = Mathf.Abs(s.y) * (aimDir.x < 0 ? -1 : 1); // i hate math
            gunModel.localScale = s;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFire)
        {
            nextFire = Time.time + 1f /fireRate;
            Shoot(aimDir);
        }
    }

    void Shoot(Vector3 aimDir)
    {
        Vector3 shotDir = (muzzle.position.z == transform.position.z)
            ? muzzle.right : aimDir;

        Vector3 endPoint = muzzle.position + shotDir * range;

    if (Physics.Raycast(muzzle.position, shotDir, out var hit, range))
        {
            endPoint = hit.point;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy) enemy.TakeDamage(damage);
                
            //Debug.DrawRay(muzzle.position, shotDir * range, Color.yellow, 0.05f);

            if (impactSparks)
            {
                impactSparks.Clear(); // i had an issue with these particles getting stuck so this is a band-aid fix
                impactSparks.transform.position = hit.point;
                impactSparks.Play();    
            }
        }

                PlayShotEffects(endPoint);
    }

    void PlayShotEffects(Vector3 endPoint)
    {
        hideEffectsTime = Time.time + effectDuration;

        if (tracer)
        {
            tracer.SetPosition(0, muzzle.position);
            tracer.SetPosition(1, endPoint);
            tracerTimer = tracerFadeTime;
            tracer.enabled = true;
        }

        if (muzzleSparks) muzzleSparks.Play();

        if (muzzleFlash)
        {
            muzzleFlash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
            muzzleFlash.SetActive(true);
        }

        gunVisual.localPosition = gunRestPos - Vector3.right * recoilDistance;
    }
}
