using UnityEngine;

public class Gun : MonoBehaviour
{
    public Camera cam;
    public Transform muzzle;
    public Transform gunModel;     // optional: used to flip the gun when aiming left
    public float range = 50f;
    public float damage = 1f;
    public float fireRate = 8f;
    float nextFire;

    void Update()
    {
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
        Ray mouseRay = cam.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(mouseRay, out float dist)) return;

        Vector3 aimDir = mouseRay.GetPoint(dist) - transform.position;
        aimDir.z = 0;
        aimDir.Normalize();

        // Rotate the pivot toward the cursor
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Keep the gun from appearing upside down when aiming left
        if (gunModel)
        {
            Vector3 s = gunModel.localScale;
            s.y = Mathf.Abs(s.y) * (aimDir.x < 0 ? -1 : 1);
            gunModel.localScale = s;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFire)
        {
            nextFire = Time.time + 1f / fireRate;
            Vector3 shotDir = (muzzle.position.z == transform.position.z)
                ? muzzle.right : aimDir;
            if (Physics.Raycast(muzzle.position, shotDir, out var hit, range))
            {
                var enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy) enemy.TakeDamage(damage);
            }
            Debug.DrawRay(muzzle.position, shotDir * range, Color.yellow, 0.05f);
        }
    }
}
