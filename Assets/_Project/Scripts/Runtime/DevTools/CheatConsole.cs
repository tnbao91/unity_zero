using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Zero.DevTools
{
    public sealed class CheatConsole : MonoBehaviour
    {
        private bool _isOpen;
        private string _inputBuffer = "";
        private List<string> _logHistory = new();
        private Vector2 _scrollPosition;
        private Dictionary<string, IConsoleCommand> _commands = new();
        private CancellationTokenSource _cts;

        private void Start()
        {
            _cts = new CancellationTokenSource();
            DiscoverCommands();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void Update()
        {
            // Toggle console on tilde key
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
            }

            // Mobile: 4-finger tap to toggle
            if (Input.touchCount >= 4)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _isOpen = !_isOpen;
                }
            }
        }

        private void OnGUI()
        {
            if (!_isOpen)
                return;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height * 0.5f), "", GUI.skin.box);

            GUILayout.BeginArea(new Rect(5, 5, Screen.width - 10, Screen.height * 0.5f - 10));

            // Log output
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(Screen.height * 0.35f));
            foreach (var line in _logHistory)
            {
                GUILayout.Label(line, GUILayout.ExpandWidth(true));
            }
            GUILayout.EndScrollView();

            GUILayout.Label("Command:", GUILayout.ExpandWidth(true));

            // Input field
            GUILayout.BeginHorizontal();
            _inputBuffer = GUILayout.TextField(_inputBuffer, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Execute", GUILayout.Width(80)))
            {
                ExecuteCommand(_inputBuffer);
                _inputBuffer = "";
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Close (Tilde)", GUILayout.ExpandWidth(true)))
            {
                _isOpen = false;
            }

            GUILayout.EndArea();
        }

        private void DiscoverCommands()
        {
            _commands.Clear();

            // Scan all loaded assemblies for [ConsoleCommand] types
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = asm.GetTypes();
                    foreach (var type in types)
                    {
                        if (!typeof(IConsoleCommand).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                            continue;

                        var attr = type.GetCustomAttribute<ConsoleCommandAttribute>();
                        if (attr == null)
                            continue;

                        // Try to instantiate via Reflex container first
                        IConsoleCommand cmd = null;
                        try
                        {
                            var container = ContainerScope.Root;
                            if (container != null)
                            {
                                cmd = (IConsoleCommand)container.Resolve(type);
                            }
                        }
                        catch
                        {
                            // Reflex resolve failed, try default constructor
                        }

                        if (cmd == null)
                        {
                            try
                            {
                                cmd = (IConsoleCommand)Activator.CreateInstance(type);
                            }
                            catch
                            {
                                Debug.LogWarning($"[CheatConsole] Failed to instantiate {type.Name}");
                                continue;
                            }
                        }

                        if (cmd != null)
                        {
                            _commands[cmd.Name] = cmd;
                        }
                    }
                }
                catch
                {
                    // Ignore assembly scan errors
                }
            }

            Log($"[Console] Discovered {_commands.Count} commands");
        }

        private void ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            Log($"> {input}");

            var parts = input.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            // Greedy match: try two-word commands first, then one-word
            IConsoleCommand cmd = null;
            string[] args = System.Array.Empty<string>();

            if (parts.Length >= 2)
            {
                var twoWord = $"{parts[0]} {parts[1]}";
                if (_commands.TryGetValue(twoWord, out var twoWordCmd))
                {
                    cmd = twoWordCmd;
                    args = parts.Skip(2).ToArray();
                }
            }

            if (cmd == null && _commands.TryGetValue(parts[0], out var oneWordCmd))
            {
                cmd = oneWordCmd;
                args = parts.Skip(1).ToArray();
            }

            if (cmd == null)
            {
                Log($"[Console] Unknown command: {parts[0]}");
                return;
            }

            ExecuteCommandAsync(cmd, args).Forget();
        }

        private async UniTaskVoid ExecuteCommandAsync(IConsoleCommand cmd, string[] args)
        {
            try
            {
                await cmd.ExecuteAsync(args, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("[Console] Command cancelled");
            }
            catch (Exception ex)
            {
                Log($"[Console] Error: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            _logHistory.Add(message);
            if (_logHistory.Count > 100)
            {
                _logHistory.RemoveAt(0);
            }
            _scrollPosition.y = float.MaxValue;
        }
    }
}
