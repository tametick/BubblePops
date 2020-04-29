public class RayCastShooter : MonoBehaviour {


	public GameObject[] colorsGO;

	public GameObject dotPrefab;


	private bool mouseDown = false;

	private List dots;

	private List dotsPool;

	private int maxDots = 26;


	private float dotGap = 0.32f;


	// Use this for initialization

	void Start() {


		dots = new List();

		dotsPool = new List();


		var i = 0;
		var alpha = 1.0f / maxDots;
		var startAlpha = 1.0f;

		while (i < maxDots) {
			var dot = Instantiate(dotPrefab) as GameObject;
			var sp = dot.GetComponent();
			var c = sp.color;
			c.a = startAlpha - alpha;
			startAlpha -= alpha;
			sp.color = c;


			dot.SetActive(false);
			dotsPool.Add(dot);
			i++;
		}
	}

	void HandleTouchDown(Vector2 touch) {
	}


	void HandleTouchUp(Vector2 touch) {
		if (dots == null || dots.Count < 2)
			return;

		foreach (var d in dotsPool)
			d.SetActive(false);
	}

	void HandleTouchMove(Vector2 touch) {
		if (dots == null) {
			return;
		}

		dots.Clear();

		foreach (var d in dotsPool)
			d.SetActive(false);

		Vector2 point = Camera.main.ScreenToWorldPoint(touch);
		var direction = new Vector2(point.x - transform.position.x, point.y - transform.position.y);

		RaycastHit2D hit = Physics2D.Raycast(transform.position, direction);
		if (hit.collider != null) {

			dots.Add(transform.position);

			if (hit.collider.tag == "SideWall") {
				DoRayCast(hit, direction);
			} else {
				dots.Add(hit.point);
				DrawPaths();
			}
		}
	}

	void DoRayCast(RaycastHit2D previousHit, Vector2 directionIn) {

		dots.Add(previousHit.point);

		var normal = Mathf.Atan2(previousHit.normal.y, previousHit.normal.x);
		var newDirection = normal + (normal - Mathf.Atan2(directionIn.y, directionIn.x));
		var reflection = new Vector2(-Mathf.Cos(newDirection), -Mathf.Sin(newDirection));
		var newCastPoint = previousHit.point + (2 * reflection);

		//		directionIn.Normalize ();
		//		newCastPoint = new Vector2(previousHit.point.x + 2 * (-directionIn.x), previousHit.point.y + 2 * (directionIn.y));
		//		reflection = new Vector2 (-directionIn.x, directionIn.y);

		var hit2 = Physics2D.Raycast(newCastPoint, reflection);
		if (hit2.collider != null) {
			if (hit2.collider.tag == "SideWall") {
				//shoot another cast
				DoRayCast(hit2, reflection);
			} else {
				dots.Add(hit2.point);
				DrawPaths();
			}
		} else {
			DrawPaths();
		}
	}

	// Update is called once per frame
	void Update() {

		if (dots == null)
			return;

		if (Input.touches.Length > 0) {


			Touch touch = Input.touches[0];


			if (touch.phase == TouchPhase.Began) {

				HandleTouchDown(touch.position);

			} else if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended) {

				HandleTouchUp(touch.position);

			} else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) {

				HandleTouchMove(touch.position);

			}

			HandleTouchMove(touch.position);

			return;

		} else if (Input.GetMouseButtonDown(0)) {

			mouseDown = true;

			HandleTouchDown(Input.mousePosition);

		} else if (Input.GetMouseButtonUp(0)) {

			mouseDown = false;

			HandleTouchUp(Input.mousePosition);

		} else if (mouseDown) {

			HandleTouchMove(Input.mousePosition);

		}

	}


	void DrawPaths() {


		if (dots.Count > 1) {


			foreach (var d in dotsPool)

				d.SetActive(false);


			int index = 0;


			for (var i = 1; i < dots.Count; i++) {
				DrawSubPath(i - 1, i, ref index);
			}
		}
	}

	void DrawSubPath(int start, int end, ref int index) {

		var pathLength = Vector2.Distance(dots[start], dots[end]);

		int numDots = Mathf.RoundToInt((float)pathLength / dotGap);
		float dotProgress = 1.0f / numDots;

		var p = 0.0f;

		while (p < 1) {
			var px = dots[start].x + p * (dots[end].x - dots[start].x);
			var py = dots[start].y + p * (dots[end].y - dots[start].y);

			if (index < maxDots) {
				var d = dotsPool[index];
				d.transform.position = new Vector2(px, py);
				d.SetActive(true);
				index++;
			}

			p += dotProgress;
		}
	}
}
