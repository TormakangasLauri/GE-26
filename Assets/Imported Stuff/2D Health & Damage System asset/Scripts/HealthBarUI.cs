using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using ThomasDev.HealthDamageSystem;

namespace ThomasDev.HealthSystem
{
    [DisallowMultipleComponent]
    public class HealthBarUI : MonoBehaviour
    {

        [SerializeField] private Image image;
        [SerializeField] private GameObject gameobject;


        private Health health;

        private void Awake()
        {
            gameobject.TryGetComponent<Health>(out health);
        }

        private void Start()
        {
            health.OnDamaged.AddListener(OnHealthChanged);
            health.OnHealed.AddListener(OnHealthChanged);
        }

        private void OnHealthChanged(float healthCurr, float healthMax)
        {
            Debug.Log(healthCurr);
            Debug.Log(image.fillAmount);
            image.fillAmount = healthCurr / 100;
        }

    }
}