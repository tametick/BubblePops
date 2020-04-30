using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Launcher : MonoBehaviour {
	// don't allow trajetories more horizontal than this (see usage below)
	private const float MinTrajectoryY = .35f;
	private const int launchVelocity = 12;
	private const float reloadDuration = .25f;
	[SerializeField]
	private GameObject bubblePrefab;
	[SerializeField]
	private Material trajectoryMaterial;

	[SerializeField]
	private Level level;

	[SerializeField]
	// the position of the bubble currently shooting
	private Vector3 currentlyShootingPosition;

	[SerializeField]
	// the position the waiting/next bubble will be waiting at
	private Vector3 waitingPosition;

	[SerializeField]
	// how small the waiting bubble starts
	private float waitingBubbleScale;

	private Bubble current;
	private Bubble waiting;


	[SerializeField]
	private MeshRenderer markerRenderer;
	private Transform marker;

	private List<Vector3> trajectory;
	private LineRenderer trajectoryRenderer;

	private void Awake() {
		marker = markerRenderer.transform;

		trajectoryRenderer = gameObject.AddComponent<LineRenderer>();
		trajectoryRenderer.material = trajectoryMaterial;
		trajectory = new List<Vector3>();
	}

	private Bubble NewBubble() {
		// create bubbles with exponent values from the bottom row, to make it easier to match
		Vector3 linePosition = new Vector3(-1, currentlyShootingPosition.y + 1);
		RaycastHit[] hits;
		int noWalls = ~(1 << LayerMask.NameToLayer("Walls"));
		do {
			linePosition = linePosition + Vector3.up;
			hits = Physics.RaycastAll(linePosition, Vector3.right, 10, noWalls);
		} while (hits.Length == 0);
		List<int> exponents = new List<int>();
		foreach (RaycastHit bubble in hits) {
			try {
				exponents.Add(bubble.collider.GetComponent<Bubble>().exponent);
			} catch (System.NullReferenceException e) {
				Debug.LogWarning($"{e.Message}\n{e.StackTrace.ToString()}");
			}

		}

		Bubble b = Instantiate(bubblePrefab, transform).GetComponent<Bubble>();
		b.SetExponent(exponents[Random.Range(0, exponents.Count)]);

		// when we cast a ray from the bubble towards the pointer we dont want it to hit the origin bubble
		b.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
		return b;
	}

	// dont process input if not ready
	private bool isReady;
	private void Start() {
		trajectoryRenderer.startWidth = .2f;
		trajectoryRenderer.endWidth = .1f;
		trajectoryRenderer.useWorldSpace = true;
		markerRenderer.enabled = false;

		current = NewBubble();
		waiting = NewBubble();
		waiting.GetComponent<Collider>().enabled = false;

		current.transform.position = currentlyShootingPosition;
		waiting.transform.position = waitingPosition;
		waiting.transform.localScale = Vector3.one * waitingBubbleScale;

		isReady = true;
	}

	// velocity vector to give the bubble when launched
	private Vector3 velocity;
	private void Update() {
		if (isReady && !EventSystem.current.IsPointerOverGameObject()) {
			float mouseX = Input.mousePosition.x;
			float mouseY = Input.mousePosition.y;
			if (mouseX > Screen.width || mouseY > Screen.height || mouseX < 0 || mouseY < 0) {
				return;
			}

			Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mouseX, mouseY, Camera.main.nearClipPlane));
			worldPosition = new Vector3(worldPosition.x, worldPosition.y);

			if (Input.GetMouseButtonDown(0) ||
				Input.GetMouseButton(0) && (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)) {

				// show path when first pressed or while pressing&moving
				trajectory.Clear();
				Vector3 startPosition = current.transform.position;
				Vector3 direction = new Vector3(worldPosition.x - startPosition.x, worldPosition.y - startPosition.y);
				direction.Normalize();

				// dont go more horizontal than this
				if (direction.y < MinTrajectoryY) {
					return;
				} else {
					velocity = direction;
				}

				RaycastHit hit = CastRay(startPosition, direction);
				if (hit.collider != null) {
					trajectory.Add(startPosition);

					if (hit.collider.tag == "SideWall") {
						DoRayCast(hit, direction);
					} else {
						trajectory.Add(hit.point);
						DrawPaths(direction);
					}
				}

			} else if (Input.GetMouseButtonUp(0)) {
				// end - launch bubble!

				trajectory.Clear();
				trajectoryRenderer.positionCount = 0;
				markerRenderer.enabled = false;

				isReady = false;
				current.isLaunched = true;
				current.transform.parent = level.transform;
				current.SetLevel(level);

				// make collider a bit smaller, then full sized upon placement
				current.GetComponent<Rigidbody>().AddForce(velocity * launchVelocity, ForceMode.VelocityChange);
			}
		} else {
			markerRenderer.enabled = false;
		}
	}

	// cast a ray ignoring the bubble currently shooting
	private RaycastHit CastRay(Vector3 startPosition, Vector3 direction) {
		RaycastHit hit;
		int layerMask = current.gameObject.layer;
		Physics.Raycast(startPosition, direction, out hit, 1000f, ~layerMask);
		return hit;
	}


	// raycasting for bounced bubble trajectory
	private void DoRayCast(RaycastHit previousHit, Vector3 directionIn) {
		trajectory.Add(previousHit.point);

		Vector3 reflection = Vector3.Reflect(directionIn, previousHit.normal);
		RaycastHit nextHit = CastRay(previousHit.point, reflection);
		if (nextHit.collider != null) {
			if (nextHit.collider.tag == "SideWall") {
				//shoot another cast
				DoRayCast(nextHit, reflection);
			} else {
				trajectory.Add(nextHit.point);
				DrawPaths(directionIn);
			}
		} else {
			DrawPaths(directionIn);
		}
	}

	private void DrawPaths(Vector3 lastDirection) {
		trajectoryRenderer.SetPositions(trajectory.ToArray());
		trajectoryRenderer.positionCount = trajectory.Count;

		if (trajectoryRenderer.positionCount > 0) {
			markerRenderer.enabled = true;

			// this is not a perfect approximation
			Vector3 hitPos = trajectory[trajectory.Count - 1];

			// go back a little so we dont show marker on top of the bubble we hit, but before it
			int tries = 5;
			while (level.GetBubble(level.WorldToGrid(hitPos)) != null && tries-- > 0) {
				hitPos -= lastDirection.normalized * .1f;
			}

			// snap to grid
			marker.transform.position = level.GridToWorld(level.WorldToGrid(hitPos));
		}
	}

	internal void Reload() {
		DOVirtual.DelayedCall(0.1f,
			() => {
				// see if we just cleared the level
				if (level.IsCleared()) {
					level.ShowPerfect();
					enabled = false;
				}
			}
		);

		current = waiting;
		current.GetComponent<Collider>().enabled = true;
		current.transform
			.DOScale(1, reloadDuration);
		current.transform
			.DOMove(currentlyShootingPosition, reloadDuration)
			.OnComplete(() => {
				isReady = true;
				level.ShiftDownIfNeeded();
			});


		waiting = NewBubble();
		waiting.transform.position = waitingPosition;
		waiting.transform.localScale = Vector3.one * waitingBubbleScale;
		waiting.GetComponent<Collider>().enabled = false;
	}
}
