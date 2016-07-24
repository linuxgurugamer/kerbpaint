/*
This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgement in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
*/
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ColorPair = System.Collections.Generic.KeyValuePair<UnityEngine.Color,UnityEngine.Vector4>;

namespace KerbPaint
{
	public class ModulePaintable : PartModule
	{

		[KSPField]
		public string Texture;
		[KSPField]
		public string Shader = "Diffuse";

		// Some optional configs for more detailed material replacement
		[KSPField]
		public string Specific = "";
		// Specific texture replacement
		[KSPField]
		public string Fairing = "";
		[KSPField]
		public string FairingName = "";
		[KSPField]
		public bool DeepReplace = false;
		[KSPField]
		public bool StretchyFix = false;
		// Fix for stretchy tank overrides

		[KSPField (isPersistant = true)]
		public Vector4 Primary = new Vector4 (0, 0, 1, 0);
		[KSPField (isPersistant = true)]
		public Vector4 Secondary = new Vector4 (0, 0, 1, 0);
		[KSPField (isPersistant = true)]
		public Vector4 Tertiary = new Vector4 (0, 0, 1, 0);

		// Load textures from disk into dictionary as requested
		public static Dictionary<string,Texture2D> Lookup = new Dictionary<string, Texture2D> ();

		public static Texture2D ProcessTexture (string TextureName)
		{
			if (Lookup.ContainsKey (TextureName)) // Bypass if the texture exists and has been loaded
				return Lookup [TextureName];

			Texture2D tex = new Texture2D (4, 4); // Will just be a 4x4 whatever if load is missing!

			// Load the texture in (Failsafely)
			try {
				var bytes = KSP.IO.File.ReadAllBytes<KerbPaint> (TextureName, null);
				tex.LoadImage (bytes);
				tex.filterMode = FilterMode.Trilinear;
				tex.anisoLevel = 8; // Higher than standard, prevent tears
				Debug.Log ("Loaded Texture " + TextureName);
			} catch (Exception e) {
				Debug.Log ("Error Loading " + TextureName);
				Debug.LogWarning (e.ToString ());
			}

			tex.Compress (true);
			tex.Apply ();

			Lookup.Add (TextureName, tex);
			return tex;
		}

		struct Combo
		{
			public int A, B, C;

			public Combo (int _a = 0, int _b = 0, int _c = 0)
			{
				A = _a;
				B = _b;
				C = _c;
			}
		}

		static Combo[] Samples = new[] {
			new Combo (0, 0, 0), // Clear x3 (aka, Default)
			new Combo (1, 11, 9), // Red Black White
			new Combo (5, 9, 5), // Cyan white white
			new Combo (7, 0, 8), // Purple clear rose
			new Combo (9, 3, 2), // White yellow orange
			new Combo (0, 10, 1), // Clear Grey Red
			new Combo (3, 11, 3), // Yellow black Yellow
			new Combo (7, 6, 9), // Purple Blue White
			new Combo (0, 8, 1), // clear rose red
			new Combo (2, 3, 6), // Orange yellow blue
			new Combo (4, 9, 3), // Green White yellow
			new Combo (10, 0, 2) // Grey Clear Orange
		};

		static ColorPair[] presets { 
			get { 
				if (_presets != null)
					return _presets;
				_presets = new[] {
					new ColorPair (Color.clear, new Vector4 (0, 0, 0, 0)), // Clear, 0 
					new ColorPair (Color.red, new Vector4 (1, 0.75f, 1, 0.85f)), // Red, 1
					new ColorPair (new Color (0.75f, 0.4f, 0), new Vector4 (0.1f, 0.75f, 1, 0.85f)), // Orange-Brown, 2
					new ColorPair (Color.yellow, new Vector4 (0.155f, 0.75f, 1, 0.85f)), // Yellow, 3
					new ColorPair (Color.green, new Vector4 (0.3f, 0.75f, 1, 0.85f)), // Green, 4
					new ColorPair (Color.cyan, new Vector4 (0.5f, 0.75f, 1, 0.85f)), // Cyan, 5
					new ColorPair (Color.blue, new Vector4 (0.65f, 0.75f, 1, 0.85f)), // Blue, 6
					new ColorPair (new Color (0.75f, 0f, 1), new Vector4 (0.79f, 0.75f, 1, 0.85f)), // Purple, 7
					new ColorPair (new Color (1f, 0f, 0.5f), new Vector4 (0.89f, 0.75f, 1, 0.85f)), // Rose, 8
					new ColorPair (Color.white, new Vector4 (1, -1, 1.1f, 0.95f)), // White, 9
					new ColorPair (Color.grey, new Vector4 (1, -1, 0.5f, 0.95f)), // Grey, 10
					new ColorPair (Color.black, new Vector4 (1, -1, 0.15f, 0.95f)) // Black, 11	
				};
				return _presets;
			}
		}

		static ColorPair[] _presets = null;

		public override void OnStart (StartState start)
		{
			ProcessTexture (Texture); // Process our texture
//			RenderingManager.AddToPostDrawQueue (1, drawGUI);
		}

		private void OnGUI()
		{
			drawGUI ();
		}

		public static ModulePaintable currentlyPainting;

		bool hasOverridden = false;
		//private Material instanceMat;

		private float flashTime = 0f;
		private bool didSelect = false;
		public bool isDirty = true;

		private Material[] ManagedMats;

		private void SetVector (string n, Vector4 v)
		{
			foreach (Material m in ManagedMats)
				m.SetVector (n, v);
		}

		private void SetRim (float strength, float falloff)
		{
			foreach (Material m in ManagedMats) {
				m.SetColor ("_RimColor", new Color (0, 0, 1, strength));
				m.SetFloat ("_RimFalloff", 2f);
			}
		}

		private Shader paintShader;

		void Update ()
		{
			if (!hasOverridden) {
				// Get the material
				var mat = GetComponentInChildren<MeshRenderer> ().material; // Get the first material we find, hope it's right :D
				var shaderString = KSP.IO.File.ReadAllText<KerbPaint> (Shader, null);
				paintShader = mat.shader = new Material (shaderString).shader;/*UnityEngine.Shader.Find(Shader);*/
				mat.SetTexture ("_Mask", ProcessTexture (Texture));
				ManagedMats = new[] { mat };

				// I apologize for the messy code here, dealing with KSP loathing Linq

				if (DeepReplace) {
					var mrs = GetComponentsInChildren<Renderer> ();//.Where(t=> ( t is MeshRenderer || t is SkinnedMeshRenderer));//.Select(r=>r.materials).SelectMany(m=>m); // Expand and flatten
					var tList = new List<Material> ();
					foreach (Renderer r in mrs) {
						// Having trouble with aero parts misbehaving
						//if(!(r is ParticleRenderer || r is ParticleSystemRenderer)) // Refix PWings?
						//	continue;
						tList.AddRange (r.materials);
					}
					var mats = tList.ToArray ();
					ManagedMats = mats;
					foreach (Material m in mats) {
						m.shader = mat.shader; // Copy over the shader
						m.SetTexture ("_Mask", ProcessTexture (Texture));
					}
				}

				if (!String.IsNullOrEmpty (Specific)) {
					var mrs = GetComponentsInChildren<Renderer> ();
					var tList = new List<Material> ();
					foreach (Renderer r in mrs) {
						// ALL materials, fix DYJ Pwings loading
						/*if(!(r is MeshRenderer || r is SkinnedMeshRenderer || ))
							continue;*/
						foreach (Material m in r.materials) {
							if (m.mainTexture.name.Contains (Specific))
								tList.Add (m);
						}
					}

					foreach (Material m in tList)
						Debug.Log (m.mainTexture.name + " Replaced");

					var mats = tList.ToArray ();
					ManagedMats = mats;
					foreach (Material m in mats) {
						m.shader = mat.shader; // Copy over the shader
						m.SetTexture ("_Mask", ProcessTexture (Texture));
					}
				}

				hasOverridden = true;
			}

			// Fix stretchy tanks!
			if (StretchyFix && paintShader && HighLogic.LoadedSceneIsEditor)
				foreach (Material m in ManagedMats)
					m.shader = paintShader; // Stretchy tanks overrides shaders, we have to trump it

			// Update the paint
			if (isDirty) {
				isDirty = false;
				SetVector ("_AWght", Primary);
				SetVector ("_BWght", Secondary);
				SetVector ("_CWght", Tertiary);
			}
		}

		void LateUpdate ()
		{ // Used for flashing the paintable part
			if (Input.GetKey (KeyCode.P)) // Highlight paintable parts with "P"
				SetRim (0.5f, 2);
			if (Input.GetKeyUp (KeyCode.P)) // Stop highlighting parts
				SetRim (0, 2);

			if (!didSelect)
				return;

			if (flashTime > 0) {
				flashTime -= Time.deltaTime;
				flashTime = Mathf.Clamp01 (flashTime);
				var smth = Mathf.SmoothStep (0, 1, flashTime);
				SetRim (smth * smth, 2);

				if (flashTime <= 0)
					didSelect = false;
			}
		}

		public void OnMouseOver ()
		{
			if (Input.GetKeyUp (KeyCode.P)) {
				menuOpen = this;
				currentlyPainting = this;
				didSelect = true;
				flashTime = 1f;
			}
		}


		public static Rect paintWindow = new Rect (128, 128, 260, 80);

		void drawGUI ()
		{
			if (currentlyPainting != this)
				return;

			if (menuOpen == false)
				return;

			// ID'd hashcode 'should' prevent control setting update issues? Hopefully?
			GUI.skin = HighLogic.Skin;
			paintWindow = GUILayout.Window (8091 + this.GetHashCode (), paintWindow, doPaintWindow, "KerbPaint", GUILayout.Width (260), GUILayout.Height (80));
		}

		Texture2D swatch {
			get {
				if (_swatch)
					return _swatch;
				_swatch = new Texture2D (2, 2);
				_swatch.SetPixels (new[] { Color.white, Color.white, Color.white, Color.white });
				_swatch.Apply ();
				return _swatch;
			}
		}

		Texture2D _swatch;

		static Vector4 cPrimary = Vector4.zero, cSecondary = Vector4.zero, cTertiary = Vector4.zero, cGeneral = Vector4.zero;

		static int selectedChannel = 0;
		static bool showPatterns = false;
		static bool menuOpen = false;
		public static bool showAdvanced;

		void doPaintWindow (int id)
		{
			var shouldClear = false; // Flag for killing the window

			GUILayout.BeginHorizontal (); // First row, Channel to color and X-out button
			GUI.enabled = !showPatterns;
			selectedChannel = GUILayout.Toolbar (selectedChannel, new[] { "Primary", "Secondary", "Tertiary" });
			GUILayout.FlexibleSpace ();
			GUI.enabled = true;
			if (GUILayout.Button ("X", GUILayout.ExpandWidth (false))) {
				currentlyPainting = null;
				shouldClear = true;
				menuOpen = false;
			}
			GUILayout.EndHorizontal ();

			var activeSet = new[] { Primary, Secondary, Tertiary } [selectedChannel]; // Grab the data struct

			int trumpWith = -1; // ID of the preset to trump with

			var rect = GUILayoutUtility.GetRect (50, 42, GUILayout.ExpandWidth (true), GUILayout.ExpandHeight (false));
			var block = rect;
			block.width /= presets.Length;
			for (int i = 0; i < presets.Length; ++i) {
				GUI.color = presets [i].Key;
				var tColor = GUI.color;
				tColor.a = Mathf.Max (0.2f, tColor.a);
				GUI.color = tColor;

				if (showPatterns) { // Draw the sample patterns instead of the default colorings
					var sBlock = block; 
					sBlock.height /= 3;
					GUI.color = presets [Samples [i].A].Key;
					GUI.DrawTexture (sBlock, swatch, ScaleMode.StretchToFill, true);
					sBlock.y += sBlock.height;
					GUI.color = presets [Samples [i].B].Key;
					GUI.DrawTexture (sBlock, swatch, ScaleMode.StretchToFill, true);
					sBlock.y += sBlock.height;
					GUI.color = presets [Samples [i].C].Key;
					GUI.DrawTexture (sBlock, swatch, ScaleMode.StretchToFill, true);
				} else {
					GUI.DrawTexture (block, swatch, ScaleMode.StretchToFill, true);
				}
				if (GUI.Button (block, GUIContent.none, GUIStyle.none)) {
					trumpWith = i;
				}
				block.x += block.width; // Increment block over
			}
			GUI.color = Color.white; // Restore full color

			GUILayout.BeginHorizontal ();
			GUI.enabled = !showPatterns;
			showAdvanced = GUILayout.Toggle (showAdvanced, "Show Advanced");
			GUI.enabled = true;
			GUILayout.FlexibleSpace ();
			showPatterns = GUILayout.Button (showPatterns ? "Patterns" : "Solids") ? !showPatterns : showPatterns;
			GUILayout.EndHorizontal ();

			if (showAdvanced && !showPatterns) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Opacity", GUILayout.Width (80), GUILayout.ExpandWidth (false));
				activeSet.w = GUILayout.HorizontalSlider (activeSet.w, 0, 1, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Hue", GUILayout.Width (80), GUILayout.ExpandWidth (false));
				activeSet.x = GUILayout.HorizontalSlider (activeSet.x, 0, 1, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Saturation", GUILayout.Width (80), GUILayout.ExpandWidth (false));
				activeSet.y = GUILayout.HorizontalSlider (activeSet.y, -1, 1, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Value", GUILayout.Width (80), GUILayout.ExpandWidth (false));
				activeSet.z = GUILayout.HorizontalSlider (activeSet.z, 0.3f, 1.7f, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
			}


			if (trumpWith != -1)  // Handle preset overloading
			if (!showPatterns)
				activeSet = presets [trumpWith].Value;

			GUILayout.BeginHorizontal (); // Copy Paste section

			if (GUILayout.Button ("Copy"))
				cGeneral = activeSet;
			if (GUILayout.Button ("Paste"))
				activeSet = cGeneral;
			if (GUILayout.Button ("Copy Set")) {
				cPrimary = Primary;
				cSecondary = Secondary;
				cTertiary = Tertiary;
			}
			if (GUILayout.Button ("Paste Set")) {
				Primary = cPrimary;
				Secondary = cSecondary;
				Tertiary = cTertiary;
			}

			GUILayout.EndHorizontal ();

			// Apply modified properties
			switch (selectedChannel) {
			case 0:
				Primary = activeSet;
				break;
			case 1:
				Secondary = activeSet;
				break;
			case 2:
				Tertiary = activeSet;
				break;
			}

			if (trumpWith != -1) {
				if (showPatterns) {
					Primary = presets [Samples [trumpWith].A].Value;
					Secondary = presets [Samples [trumpWith].B].Value;
					Tertiary = presets [Samples [trumpWith].C].Value;
				}
			}

			// Ideally I should move this out of the window update, but there isn't any otherwise nice solution I can think of
			foreach (Part p in this.part.symmetryCounterparts) { // Based on DYJ's P-dynamics and advice :)
				var clone = p.Modules.OfType<ModulePaintable> ().FirstOrDefault ();

				clone.isDirty = true; // Constantly dirty symmetrically copied parts!
				clone.Primary = Primary;
				clone.Secondary = Secondary;
				clone.Tertiary = Tertiary;
			}

			isDirty = true; // Dirty the paint so it updates

			if (shouldClear)
				currentlyPainting = null;
			GUI.DragWindow ();
		}
	}

	public class KerbPaint
	{

	}
}
