using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Bubble : MonoBehaviour {
	// the power of 2 this bubble contains
	internal int exponent { get; private set; }
	private TextMeshPro label;
	// the colors of exponents
	[SerializeField]
	private Color[] colors;
	// effect when popping bubble
	[SerializeField]
	private GameObject particleEffectPrefab;
	private GameObject particleEffect;

	// logical position in the level grid
	private IntVector2 gridPosition;
	private new Collider collider;
	// ref to level for checking neighboring bubbles
	private Level level;

	// which bubble I'm connected to (used for dropping "islands" of bubbles)
	private FixedJoint anchor;
	// which bubbles connect to me
	internal List<Bubble> connectedToMe = new List<Bubble>();

	// is this bubble currently merging
	private bool isMerging;

	private void Awake() {
		label = GetComponentInChildren<TextMeshPro>();
		collider = GetComponent<Collider>();
		SetUpParticleEffect();
	}

	private void SetUpParticleEffect() {
		particleEffect = Instantiate(particleEffectPrefab, transform);
		particleEffect.SetActive(false);
	}

	internal void Init(Level level, IntVector2 gridPosition, Bubble anchorBubble, int exponent) {
		EnterLevel(level, gridPosition);

		Attach(anchorBubble);
		SetExponent(exponent);
		// gravity is used for dropping bubbles post-detachment
		GetComponent<Rigidbody>().useGravity = true;
	}

	internal void SetLevel(Level level) {
		this.level = level;
	}

	internal void EnterLevel(Level level, IntVector2 gridPosition) {
		this.level = level;
		this.gridPosition = gridPosition;
		name = name.Replace("(Clone)", $" {gridPosition.ToString()}");
	}

	private void Attach(Bubble anchorBubble) {
		SetTrigger(false);
		anchor = gameObject.AddComponent<FixedJoint>();
		if (anchorBubble != null) {
			anchor.connectedBody = anchorBubble.GetComponent<Rigidbody>();
			anchorBubble.connectedToMe.Add(this);
		} else {
			// if no bubble is given, connect to the level
			anchor.connectedBody = level.GetComponent<Rigidbody>();
		}
	}

	internal void Detach() {
		// break connection to my anchor
		anchor.breakForce = 0;
		if (anchor.connectedBody != null) {
			try {
				Bubble bubble = anchor.connectedBody.GetComponent<Bubble>();
				if (bubble != null && bubble.connectedToMe != null) {
					bubble.connectedToMe.Remove(this);
				}
			} catch (System.NullReferenceException e) {
				Debug.LogWarning($"{e.Message}\n{e.StackTrace.ToString()}");
			}

		}
		anchor.connectedBody = null;
		
		// make this bubble not bounce off of the sidewalls
		SetTrigger(true);
	}

	internal static int GetRandomExponent() {
		return 1 + UnityEngine.Random.Range(0, 10);
	}

	internal void SetExponent(int exponent) {
		this.exponent = exponent;
		label.text = exponent < 10 ? Mathf.Pow(2, exponent).ToString() : "1K";
		GetComponentInChildren<SpriteRenderer>().material.color = colors[exponent - 1];
	}

	internal void RemoveSelf() {
		level.RemoveBubble(gridPosition);
	}

	private void OnJointBreak(float breakForce) {
		if (isMerging) {
			RemoveSelf();
			PlayParticleEffect();
		} else {
			// see if we can attach to another neighbor
			Bubble newAnchor = GetNewAnchor();
			if (newAnchor != null) {
				Attach(newAnchor);
			} else {
				// we are now free falling
				DetachConnectedToMe();
				RemoveSelf();
			}
		}
	}

	private void PlayParticleEffect() {
		ParticleSystem.MainModule ps1 = particleEffect.GetComponent<ParticleSystem>().main;
		ParticleSystem.MainModule ps2 = particleEffect.transform.GetChild(0).GetComponent<ParticleSystem>().main;
		ps1.startColor = colors[exponent - 1];
		ps2.startColor = colors[exponent - 1];
		particleEffect.SetActive(true);
		particleEffect.transform.position = transform.position - Vector3.forward;
		particleEffect.transform.parent = transform.parent;
		// destroy after finished playing
		DOVirtual.DelayedCall(1, () => {
			Destroy(particleEffect);
		});
	}

	void OnDrawGizmos() {
		if (anchor != null && anchor.connectedBody != null) {
			Gizmos.color = Color.yellow;
			var attachedFrom = transform;
			var attachedTo = anchor.connectedBody.transform;
			if (attachedTo.GetComponent<Bubble>() != null)
				Gizmos.DrawLine(attachedFrom.position, attachedTo.position);
		}
	}


	// try to find new anchor bubble to attach to
	internal Bubble GetNewAnchor() {
		List<Bubble> neighbors = level.GetNeighbors(gridPosition.x, gridPosition.y);
		// and those not already connected to me
		neighbors = neighbors.FindAll(n => !connectedToMe.Contains(n));

		// attach to the first valid neighbor (the list is sorted in order of preference)
		if (neighbors.Count > 0) {
			return neighbors[0];
		} else {
			return null;
		}
	}

	internal bool isLaunched;
	private void OnCollisionEnter(Collision collision) {
		string colName = collision.gameObject.name;
		// don't stop if you collided into the side walls, they'll bounce you instead
		if (!isLaunched || colName.StartsWith("leftWall") || colName.StartsWith("rightWall")) {
			return;
		}

		// after we detect where the launched bubble ends up we add it to the level and start merging
		IntVector2 gridPos = level.WorldToGrid(transform.position);
		if (level.GetBubble(gridPos.x, gridPos.y) == null) {
			gameObject.layer = LayerMask.NameToLayer("Default");
			transform.position = level.GridToWorld(gridPos);

			// GetNewAnchor requires this info, that's why it's also done here even tho it's also called inside Init
			EnterLevel(level, gridPos);
			level.AddBubble(gridPos, this);

			Init(level, gridPos, GetNewAnchor(), exponent);
			// if no merges possible, reload now - otherwise it will reload after merging
			if (SearchForMerge() == 0) {
				level.launcher.Reload();
			}
			isLaunched = false;
		}
	}

	// search for suitable bubbles & merge them into this one
	// return how many were found
	internal int SearchForMerge() {
		List<Bubble> sameExponentNeighbors = GetSameExponentNeighbors();

		if (sameExponentNeighbors.Count > 0) {
			// if there are several neighbors i can merge into, merge into the one with the most same-color neighbors
			// (bubbles prefer to merge into a location where an automatic new merge is possible)
			if (sameExponentNeighbors.Count > 1) {
				sameExponentNeighbors.Sort(ComparisonByMostNeighborsOfSameExponent);
			}

			Bubble mergingTo = sameExponentNeighbors[0];
			mergingTo.MergeNeighborsIntoMe();
		}

		return sameExponentNeighbors.Count;
	}

	private void DetachConnectedToMe() {
		foreach (Bubble connected in connectedToMe.ToArray()) {
			if (connected != null) {
				connected.Detach();
			}
		}
	}

	internal void SetTrigger(bool t) {
		collider.isTrigger = t;
	}

	private void MergeNeighborsIntoMe() {
		List<Bubble> sameExponentNeighbors = GetSameExponentNeighbors();
		int newExponent = exponent + sameExponentNeighbors.Count;
		const float mergeDuration = 0.15f;

		foreach (Bubble neighbor in sameExponentNeighbors) {
			// detach neighbor and all bubbles connected to it & start merging
			neighbor.MarkMerging();
			neighbor.GetComponent<Rigidbody>().useGravity = false;
			// these will try to find a new anchor if they are not merging themselves
			neighbor.DetachConnectedToMe();
			neighbor.Detach();

			// move into the merged bubble
			neighbor.transform
				.DOMove(transform.position, mergeDuration)
				.OnComplete(neighbor.FinishMergng);
		}
		DOVirtual.DelayedCall(mergeDuration, () => FinishMergeIntoMe(newExponent));
	}

	private void FinishMergeIntoMe(int newExponent) {
		if (newExponent <= 10) {
			SetExponent(newExponent);
		} else {
			List<Bubble> bubblesToExplode = level.GetNeighbors(gridPosition.x, gridPosition.y);
			while (bubblesToExplode.Count > 0) {
				Bubble explodeMe = bubblesToExplode[0];
				bubblesToExplode.RemoveAt(0);
				explodeMe.Explode();
			}
			Explode();
		}

		if (SearchForMerge() == 0) {
			level.FinishedMerging();
		}
	}

	private void Explode() {
		// effects
		PlayParticleEffect();

		// don't leave other bubbles hanging
		DetachConnectedToMe();
		// making sure we don't leave a reference behind
		RemoveSelf();
		Destroy(gameObject);
	}

	private void FinishMergng() {
		isMerging = false;
		// making sure we don't leave a reference behind
		RemoveSelf();
		Destroy(gameObject);
	}

	private void MarkMerging() {
		isMerging = true;
	}

	private List<Bubble> GetSameExponentNeighbors() {
		return level
				.GetNeighbors(gridPosition.x, gridPosition.y)
				.FindAll(n => n.exponent == exponent);
	}

	private static int ComparisonByMostNeighborsOfSameExponent(Bubble b1, Bubble b2) {
		int b1Neighbors = b1.GetSameExponentNeighbors().Count;
		int b2Neighbors = b2.GetSameExponentNeighbors().Count;
		// comparing backwards to get those with largest number of mergable-neighbors first 
		return b2Neighbors.CompareTo(b1Neighbors);
	}

}
