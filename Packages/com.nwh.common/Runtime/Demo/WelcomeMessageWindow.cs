// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.NUI;
using UnityEditor;
using UnityEngine;

#endregion

namespace NWH.Common.AssetInfo
{
    /// <summary>
    /// EditorWindow displaying welcome message with package information and useful links.
    /// Shown on first import or version update via CommonInitializationMethods.
    /// </summary>
    public class WelcomeMessageWindow : EditorWindow
    {
        /// <summary>
        /// AssetInfo containing package metadata and URLs to display.
        /// </summary>
        public AssetInfo assetInfo;

        /// <summary>
        /// Callback invoked when window is closed. Used to trigger next window in queue.
        /// </summary>
        internal System.Action onCloseCallback;

        /// <summary>
        /// Currently selected sidebar category index.
        /// </summary>
        private int selectedSidebarIndex = 0;

        /// <summary>
        /// Cached logo texture for package branding.
        /// </summary>
        private Texture2D logoTexture;

        /// <summary>
        /// Scroll position for content area.
        /// </summary>
        private Vector2 scrollPosition;


        /// <summary>
        /// Unity callback when window is enabled.
        /// Logo loading moved to OnGUI due to Unity lifecycle (assetInfo not set until after OnEnable).
        /// </summary>
        private void OnEnable()
        {
            // Logo will be loaded lazily in OnGUI when assetInfo is available
        }


        /// <summary>
        /// Loads package-specific logo texture with fallback to generic NWH logo.
        /// </summary>
        private void LoadLogoTexture()
        {
            if (assetInfo == null)
            {
                return;
            }

            // Try to load package-specific logo based on asset name
            string assetName = assetInfo.assetName;
            string logoPath = null;

            if (assetName.Contains("Vehicle Physics"))
            {
                logoPath = "NWH Vehicle Physics 2/Editor/logo_light";
            }
            else if (assetName.Contains("Dynamic Water Physics"))
            {
                logoPath = "Dynamic Water Physics 2/Logos/dwp_logo";
            }
            else if (assetName.Contains("Aerodynamics"))
            {
                logoPath = "NWH Aerodynamics/Editor/NAE Logo";
            }
            else if (assetName.Contains("Wheel Controller"))
            {
                logoPath = "Wheel Controller 3D/Editor/logo_wc3d_light";
            }

            if (!string.IsNullOrEmpty(logoPath))
            {
                logoTexture = Resources.Load<Texture2D>(logoPath);
            }

            // Fallback to generic NWH logo
            if (logoTexture == null)
            {
                logoPath = "Editor/NWHLogoSquare";
                logoTexture = Resources.Load<Texture2D>(logoPath);
            }
        }


        /// <summary>
        /// Draws welcome message GUI with package info, documentation links, and support resources.
        /// </summary>
        /// <param name="assetInfo">AssetInfo containing package metadata</param>
        /// <param name="width">Window width in pixels</param>
        public static void DrawWelcomeMessage(AssetInfo assetInfo, float width = 300f)
        {
            if (assetInfo == null)
            {
                Debug.LogWarning("AssetInfo is null");
                return;
            }

            GUIStyle style = new(EditorStyles.helpBox);
            style.margin  = new RectOffset(10, 10, 10, 12);
            style.padding = new RectOffset(10, 10, 10, 12);

            GUILayout.BeginVertical(style, GUILayout.Width(width - 35f));
            GUILayout.Space(8);
            GUILayout.Label($"Welcome to {assetInfo.assetName}", EditorStyles.boldLabel);
            GUILayout.Space(15);
            GUILayout.Label($"Thank you for purchasing {assetInfo.assetName}.\n" +
                            "Check out the following links:");
            GUILayout.Space(10);
            GUILayout.Label("Existing customer?", EditorStyles.centeredGreyMiniLabel);
            if (GUILayout.Button("Upgrade Notes"))
            {
                Application.OpenURL(assetInfo.upgradeNotesURL);
            }

            if (GUILayout.Button("Changelog"))
            {
                Application.OpenURL(assetInfo.changelogURL);
            }

            GUILayout.Space(5);
            GUILayout.Label("New to the asset?", EditorStyles.centeredGreyMiniLabel);
            if (GUILayout.Button("Quick Start"))
            {
                Application.OpenURL(assetInfo.quickStartURL);
            }

            if (GUILayout.Button("Documentation"))
            {
                Application.OpenURL(assetInfo.documentationURL);
            }

            GUILayout.Space(15);
            GUILayout.Label("Also, don't forget to join us at Discord:", EditorStyles.centeredGreyMiniLabel);
            if (GUILayout.Button("Discord Server"))
            {
                Application.OpenURL(assetInfo.discordURL);
            }

            GUILayout.Space(15);
            GUILayout.Label("Don't have Discord? You can also contact us through:", EditorStyles.centeredGreyMiniLabel);

            if (GUILayout.Button("Email"))
            {
                Application.OpenURL(assetInfo.emailURL);
            }

            if (GUILayout.Button("Forum"))
            {
                Application.OpenURL(assetInfo.forumURL);
            }

            GUILayout.Space(15);
            GUILayout.Label("Enjoying the asset? Please consider leaving a review, \n" +
                            "it means a lot to us developers. Thank you.", EditorStyles.centeredGreyMiniLabel);
            if (GUILayout.Button("Leave a Review"))
            {
                Application.OpenURL(assetInfo.assetURL);
            }

            GUILayout.EndVertical();
        }


        /// <summary>
        /// Unity callback to draw window GUI.
        /// </summary>
        private void OnGUI()
        {
            if (assetInfo == null)
            {
                return;
            }

            // Lazy load logo on first draw if needed
            if (logoTexture == null)
            {
                LoadLogoTexture();
            }

            // Set minimum window size
            minSize = new Vector2(650f, 450f);

            // Draw logo at top
            DrawLogoSection();

            // Draw main content area (sidebar + content)
            EditorGUILayout.BeginHorizontal();

            // Draw sidebar
            DrawSidebar();

            // Draw content based on selection
            DrawContent();

            EditorGUILayout.EndHorizontal();
        }


        /// <summary>
        /// Draws logo section at top of window with background and padding.
        /// </summary>
        private void DrawLogoSection()
        {
            // Background box for logo section with NWH brand color
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = NUISettings.editorHeaderColor;

            GUIStyle logoBackgroundStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(20, 20, 15, 15)
            };

            EditorGUILayout.BeginVertical(logoBackgroundStyle, GUILayout.Height(120));
            GUILayout.FlexibleSpace();

            if (logoTexture != null)
            {
                Rect logoRect = GUILayoutUtility.GetRect(100, 90, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback: show asset name if logo fails to load
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                GUILayout.Label(assetInfo.assetName, titleStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = originalColor;
            GUILayout.Space(5);
        }


        /// <summary>
        /// Draws sidebar with category selection buttons.
        /// </summary>
        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(200));

            GUILayout.Space(10);
            GUILayout.Label("Categories", EditorStyles.boldLabel);
            GUILayout.Space(15);

            string[] categories = { "Getting Started", "What's New", "Documentation", "Support & Community", "Other Assets" };

            for (int i = 0; i < categories.Length; i++)
            {
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 8, 8),
                    fontStyle = selectedSidebarIndex == i ? FontStyle.Bold : FontStyle.Normal
                };

                // NWH brand color for selected item
                if (selectedSidebarIndex == i)
                {
                    Color originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = NUISettings.lightBlueColor;
                    if (GUILayout.Button(categories[i], buttonStyle, GUILayout.Height(32)))
                    {
                        selectedSidebarIndex = i;
                    }
                    GUI.backgroundColor = originalColor;
                }
                else
                {
                    if (GUILayout.Button(categories[i], buttonStyle, GUILayout.Height(32)))
                    {
                        selectedSidebarIndex = i;
                    }
                }

                GUILayout.Space(5);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }


        /// <summary>
        /// Draws content area based on selected sidebar category.
        /// </summary>
        private void DrawContent()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUILayout.Space(10);

            switch (selectedSidebarIndex)
            {
                case 0: // Getting Started
                    DrawGettingStartedContent();
                    break;
                case 1: // What's New
                    DrawWhatsNewContent();
                    break;
                case 2: // Documentation
                    DrawDocumentationContent();
                    break;
                case 3: // Support & Community
                    DrawSupportContent();
                    break;
                case 4: // Other Assets
                    DrawOtherAssetsContent();
                    break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }


        /// <summary>
        /// Draws Getting Started category content.
        /// </summary>
        private void DrawGettingStartedContent()
        {
            // Welcome section
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label($"Welcome to {assetInfo.assetName}!", headerStyle);
            GUILayout.Space(8);

            GUILayout.Label($"Thank you for purchasing {assetInfo.assetName}. " +
                          "We're excited to have you on board!", EditorStyles.wordWrappedLabel);
            GUILayout.Space(20);

            // Quick Start section
            DrawSectionHeader("Quick Start");
            GUILayout.Label("Get up and running quickly with our comprehensive Quick Start guide.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("Open Quick Start Guide", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.quickStartURL);
            }

            GUILayout.Space(25);

            // Existing Customer section
            DrawSectionHeader("Existing Customer?");
            GUILayout.Label("Already familiar with the asset? Check what's new in this version.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("View Upgrade Notes", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.upgradeNotesURL);
            }
        }


        /// <summary>
        /// Draws Documentation category content.
        /// </summary>
        private void DrawDocumentationContent()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label("Documentation & Resources", headerStyle);
            GUILayout.Space(15);

            // Full Documentation section
            DrawSectionHeader("Full Documentation");
            GUILayout.Label("Complete reference for all features, components, and API documentation.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("Open Documentation", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.documentationURL);
            }

            GUILayout.Space(25);

            // Changelog section
            DrawSectionHeader("Changelog");
            GUILayout.Label("View complete history of changes, fixes, and improvements across all versions.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("View Changelog", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.changelogURL);
            }
        }


        /// <summary>
        /// Draws Support & Community category content.
        /// </summary>
        private void DrawSupportContent()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label("Support & Community", headerStyle);
            GUILayout.Space(15);

            // Discord Community section
            DrawSectionHeader("Discord Community");
            GUILayout.Label("Join our Discord server for quick help, discussions, and community support.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("Join Discord Server", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.discordURL);
            }

            GUILayout.Space(25);

            // Direct Support section
            DrawSectionHeader("Direct Support");
            GUILayout.Label("Contact us directly for technical support and assistance.",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Email Support", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.emailURL);
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Forum", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.forumURL);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(25);

            // Review section
            DrawSectionHeader("Enjoying the Asset?");
            GUILayout.Label("Please consider leaving a review. It means a lot to us developers and helps others discover our work!",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            if (GUILayout.Button("Leave a Review on Asset Store", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.assetURL);
            }
        }


        /// <summary>
        /// Draws What's New category content showing recent updates.
        /// </summary>
        private void DrawWhatsNewContent()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label($"What's New in {assetInfo.assetName}", headerStyle);
            GUILayout.Space(8);

            // Version info
            GUIStyle versionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic
            };
            GUILayout.Label($"Version {assetInfo.version}", versionStyle);
            GUILayout.Space(15);

            // Recent updates
            if (assetInfo.recentUpdates != null && assetInfo.recentUpdates.Length > 0)
            {
                DrawSectionHeader("Recent Updates");
                GUILayout.Space(5);

                foreach (string update in assetInfo.recentUpdates)
                {
                    if (!string.IsNullOrEmpty(update))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("•", GUILayout.Width(15));
                        GUILayout.Label(update, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(5);
                    }
                }

                GUILayout.Space(20);

                // Link to full changelog
                GUILayout.Label("For complete version history and detailed changes:",
                              EditorStyles.wordWrappedLabel);
                GUILayout.Space(8);
                if (GUILayout.Button("View Full Changelog", GUILayout.Height(35)))
                {
                    Application.OpenURL(assetInfo.changelogURL);
                }
            }
            else
            {
                GUILayout.Label("No recent updates information available.",
                              EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(15);
                if (GUILayout.Button("View Full Changelog", GUILayout.Height(35)))
                {
                    Application.OpenURL(assetInfo.changelogURL);
                }
            }
        }


        /// <summary>
        /// Draws Other Assets category content showing other NWH products.
        /// </summary>
        private void DrawOtherAssetsContent()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label("Other NWH Assets", headerStyle);
            GUILayout.Space(15);

            GUILayout.Label("Explore our other high-quality Unity assets:",
                          EditorStyles.wordWrappedLabel);
            GUILayout.Space(20);

            // Define other NWH assets (excluding current one)
            string currentAsset = assetInfo.assetName;

            if (!currentAsset.Contains("Vehicle Physics"))
            {
                DrawAssetCard("NWH Vehicle Physics 2",
                            "Complete vehicle physics solution with realistic wheel physics, sound, damage, and more.",
                            "https://assetstore.unity.com/packages/tools/physics/nwh-vehicle-physics-2-166252?aid=1011ljhgE");
            }

            if (!currentAsset.Contains("Dynamic Water Physics"))
            {
                DrawAssetCard("Dynamic Water Physics 2",
                            "Advanced water simulation with buoyancy, ship controllers, and realistic water interactions.",
                            "https://assetstore.unity.com/packages/tools/physics/dynamic-water-physics-2-147990?aid=1011ljhgE");
            }

            if (!currentAsset.Contains("Wheel Controller"))
            {
                DrawAssetCard("Wheel Controller 3D",
                            "Standalone wheel physics controller with advanced tire friction and suspension simulation.",
                            "https://assetstore.unity.com/packages/tools/physics/wheel-controller-3d-49574?aid=1011ljhgE");
            }

            if (!currentAsset.Contains("Aerodynamics"))
            {
                DrawAssetCard("NWH Aerodynamics",
                            "Realistic aerodynamics simulation for aircraft and vehicles with customizable airfoils.",
                            "https://assetstore.unity.com/packages/tools/physics/nwh-aerodynamics-288831?aid=1011ljhgE");
            }

            GUILayout.Space(15);

            // Link to publisher page
            DrawSectionHeader("View All Assets");
            if (GUILayout.Button("Visit NWH Publisher Page", GUILayout.Height(35)))
            {
                Application.OpenURL(assetInfo.publisherURL);
            }
        }


        /// <summary>
        /// Helper method to draw a section header with consistent styling.
        /// </summary>
        private void DrawSectionHeader(string title)
        {
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            Color originalColor = GUI.contentColor;
            GUI.contentColor = NUISettings.propertyHeaderColor;
            GUILayout.Label(title, sectionStyle);
            GUI.contentColor = originalColor;
        }


        /// <summary>
        /// Helper method to draw an asset card with name, description, and link.
        /// </summary>
        private void DrawAssetCard(string assetName, string description, string url)
        {
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            EditorGUILayout.BeginVertical(cardStyle);

            GUILayout.Label(assetName, EditorStyles.boldLabel);
            GUILayout.Space(5);
            GUILayout.Label(description, EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);

            if (GUILayout.Button("View on Asset Store", GUILayout.Height(30)))
            {
                Application.OpenURL(url);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(12);
        }


        /// <summary>
        /// Unity callback when window is destroyed. Triggers next window in queue.
        /// </summary>
        private void OnDestroy()
        {
            onCloseCallback?.Invoke();
        }
    }
}
#endif