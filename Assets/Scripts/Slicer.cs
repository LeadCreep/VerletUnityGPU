using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Slicer : MonoBehaviour
{
	private CapsuleCollider capsuleCollider;
	private Camera cam;

	void Awake()
	{
		capsuleCollider = GetComponent<CapsuleCollider>();
		cam = Camera.main;
	}

	void Update()
	{
		if (Input.GetKey(KeyCode.Mouse0))
		{
			Vector3 mousePos = Input.mousePosition;
			Vector3 lookAt = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, capsuleCollider.height));
			Vector3 direction = ( lookAt - cam.transform.position ).normalized;
			Debug.DrawRay(cam.transform.position, direction * 100f, Color.red);
			// Put the capsule collider on the ray
			Ray ray = new Ray(cam.transform.position, direction * capsuleCollider.height);
			Vector3 rayMiddlePoint = ray.GetPoint(capsuleCollider.height / 2f);
			capsuleCollider.transform.position = rayMiddlePoint;
			capsuleCollider.transform.rotation = Quaternion.LookRotation(direction);
		}
	}
}
