using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace ProceduralSphere
{
	public class UvSphere : ScriptableWizard
	{
		public enum Materials
		{
			DefaultMaterial,
			TestMaterial
		}

		public enum Direction
		{
			NegativeZ,
			PositiveZ,
			NegativeX,
			PositiveX
		}

		public Materials material = Materials.DefaultMaterial;
		public bool flipNormals = false;
		public bool flipUVHorizontal = false;
		public Direction frontDirection = Direction.NegativeZ;
		public float radius = 0.5f;
		public int stacks = 32;
		public int slices = 32;

		private const string meshesDirectory = "Assets/ProceduralSphere/Meshes/";
		private GameObject sphere;
		private Materials previousMaterial = Materials.DefaultMaterial;
		private bool createFinish;

		[MenuItem ("GameObject/3D Object/UV Sphere")]
		static void CreateWizard ()
		{
			DisplayWizard<UvSphere> ("UV Sphere", "Create");
		}

		void OnHierarchyChange ()
		{
			if (!Exists (sphere)) {
				isValid = false;
				errorString = "GameObject has been deleted.";
			}
		}

		void Awake ()
		{
			minSize = new Vector2 (270f, 230f);
			sphere = GenerateSphere ();
		}

		void OnWizardCreate ()
		{
			Undo.RegisterCreatedObjectUndo (sphere, "Create Sphere");

			Mesh mesh = sphere.GetComponent<MeshFilter> ().sharedMesh;
			Mesh loadMesh = AssetDatabase.LoadAssetAtPath<Mesh> (meshesDirectory + mesh.name + ".asset");
			if (loadMesh != null) {
				sphere.GetComponent<MeshFilter> ().mesh = loadMesh;
			} else {
				if (!Directory.Exists (meshesDirectory)) {
					Directory.CreateDirectory (meshesDirectory);
				}
				AssetDatabase.CreateAsset (mesh, meshesDirectory + mesh.name + ".asset");
				AssetDatabase.SaveAssets ();
			}

			createFinish = true;
		}

		void OnWizardUpdate ()
		{
			if (!Exists (sphere)) {
				return;
			}

			radius = (radius <= 0) ? 0.001f : radius;
			stacks = (stacks < 3) ? 3 : stacks;
			slices = (slices < 3) ? 3 : slices;

			int verts = (stacks + 1) * (slices + 1);
			int tris = stacks * slices * 2;
			helpString = string.Format ("{0} verts, {1} tris", verts, tris);
			if (verts > 65000) {
				errorString = "Mesh.vertices is too large. A mesh may not have more than 65000 vertices.";
				isValid = false;
				return;
			} else {
				errorString = "";
				isValid = true;
			}

			sphere.GetComponent<MeshFilter> ().mesh = GenerateMesh ();

			if (material == previousMaterial) {
				return;
			}
			switch (material) {
			case Materials.DefaultMaterial:
				sphere.GetComponent<Renderer> ().material = AssetDatabase.GetBuiltinExtraResource<Material> ("Default-Material.mat");
				break;
			case Materials.TestMaterial:
				sphere.GetComponent<Renderer> ().material = AssetDatabase.LoadAssetAtPath<Material> ("Assets/ProceduralSphere/Test-Material.mat");
				break;
			}
			previousMaterial = material;
		}

		void OnDestroy ()
		{
			if (!createFinish) {
				DestroyImmediate (sphere);
			}
		}

		private GameObject GenerateSphere ()
		{
			// Sphere, Sphere (1), Sphere (2)...
			string gameObjectName = "Sphere";
			int suffixNumber = 0;
			while (true) {
				if (GameObject.Find (gameObjectName) == null) {
					break;
				} else {
					suffixNumber++;
					gameObjectName = string.Format ("Sphere ({0})", suffixNumber);
				}
			}

			GameObject sphereObject = new GameObject (gameObjectName, typeof(MeshFilter), typeof(MeshRenderer));
			if (Selection.activeGameObject) {
				sphereObject.transform.parent = Selection.activeGameObject.transform;
				sphereObject.transform.localPosition = Vector3.zero;
				sphereObject.transform.localRotation = Quaternion.identity;
			} else {
				sphereObject.transform.position = SceneView.lastActiveSceneView.pivot;
			}
			Selection.activeGameObject = sphereObject;

			sphereObject.GetComponent<MeshFilter> ().mesh = GenerateMesh ();
			sphereObject.GetComponent<Renderer> ().material = AssetDatabase.GetBuiltinExtraResource<Material> ("Default-Material.mat");

			return sphereObject;
		}

		private Mesh GenerateMesh ()
		{
			int vertexCount = (stacks + 1) * (slices + 1);
			Vector3[] vertices = new Vector3[vertexCount];
			Vector2[] uv = new Vector2[vertexCount];
			Vector3[] normal = new Vector3[vertexCount];
			List<int> indecies = new List<int> ();
			Mesh mesh = new Mesh ();

			// Sphere_radius0.5_stacks16_slices16_flipNormFalse_flipUvFalse_NegativeZ
			mesh.name = string.Format (
				"Sphere" +
				"_radius{0}" +
				"_stacks{1}" +
				"_slices{2}" +
				"_flipNorm{3}" +
				"_flipUv{4}" +
				"_{5}", radius, stacks, slices, flipNormals, flipUVHorizontal, frontDirection);

			int index = 0;
			for (int stack = 0; stack <= stacks; stack++) {
				float thetaV = Mathf.PI * ((float)stack / (float)stacks);
				float r = radius * Mathf.Sin (thetaV);
				float y = radius * Mathf.Cos (thetaV);
				for (int slice = 0; slice <= slices; slice++) {
					float thetaH = 2.0f * Mathf.PI * ((float)slice / (float)slices);
					float x = r * Mathf.Cos (thetaH);
					float z = r * Mathf.Sin (thetaH);
					vertices [index] = new Vector3 (x, y, z);

					float u = (float)slice / (float)slices;
					if (flipUVHorizontal) {
						u = 1.0f - u;
					}
					float v = (float)stack / (float)stacks;
					uv [index] = new Vector2 (u, 1.0f - v);

					index++;
				}
			}

			for (int stack = 0; stack < stacks; stack++) {
				for (int slice = 0; slice < slices; slice++) {
					int count = slice + ((slices + 1) * stack);

					indecies.Add (count);
					indecies.Add (count + 1);
					indecies.Add (count + slices + 2);

					indecies.Add (count);
					indecies.Add (count + slices + 2);
					indecies.Add (count + slices + 1);
				}
			}

			Matrix4x4 matrix = Matrix4x4.identity;
			float rotationY = 0f;
			switch (frontDirection) {
			case Direction.NegativeZ:
				rotationY = -90f;
				break;
			case Direction.PositiveZ:
				rotationY = 90f;
				break;
			case Direction.PositiveX:
				rotationY = 180f;
				break;
			}
			matrix.SetTRS (Vector3.zero, Quaternion.Euler (0f, rotationY, 0f), Vector3.one);
			for (int i = 0; i < vertexCount; i++) {
				vertices [i] = matrix.MultiplyPoint3x4 (vertices [i]);
			}

			for (int i = 0; i < vertices.Length; i++) {
				normal [i] = vertices [i].normalized;
			}

			if (flipNormals) {
				indecies.Reverse ();
				for (int i = 0; i < vertices.Length; i++) {
					normal [i] *= -1;
				}
			}

			mesh.vertices = vertices;
			mesh.uv = uv;
			mesh.normals = normal;
			mesh.triangles = indecies.ToArray ();
			mesh.Optimize ();
			mesh.RecalculateBounds ();

			return mesh;
		}

		private bool Exists (GameObject gameObject)
		{
			gameObject.GetInstanceID ();
			if (gameObject != null) {
				return true;
			} else {
				return false;
			}
		}
	}
}
