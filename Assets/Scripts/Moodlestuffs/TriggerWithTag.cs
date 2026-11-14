using UnityEngine;
using UnityEngine.Events;

public class TriggerWithTag : MonoBehaviour
{
    public string tagToCheck = "Player";
	public UnityEvent onTriggerEvent;

	private void OnTriggerEnter(Collider other)
	{
		if (other.gameObject.CompareTag(tagToCheck))
		{
			Debug.Log($"hit {tagToCheck}");
			// You can do here things you want to trigger
			onTriggerEvent.Invoke();
		}
	}
}
