using UnityEngine;

public class RemoveFalling : MonoBehaviour {
	// remove falling bubbles from the game
	private void OnTriggerEnter(Collider other) {
		other.GetComponent<Bubble>().RemoveSelf();
		Destroy(other.gameObject);
	}
	private void OnCollisionEnter(Collision collision) {
		collision.gameObject.GetComponent<Bubble>().RemoveSelf();
		Destroy(collision.gameObject);
	}
}
