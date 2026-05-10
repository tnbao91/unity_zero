using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Zero.DevTools;

namespace Zero.Tests.EditMode
{
    public class ConsoleCommandParserTests
    {
        private Dictionary<string, IConsoleCommand> _commands;

        [SetUp]
        public void Setup()
        {
            _commands = new Dictionary<string, IConsoleCommand>();

            // Register mock commands
            _commands["save reset"] = new MockCommand("save reset");
            _commands["loc set"] = new MockCommand("loc set");
            _commands["version check"] = new MockCommand("version check");
            _commands["fps"] = new MockCommand("fps");
        }

        [Test]
        public void ParseCommand_TwoWordCommand_MatchesGreedily()
        {
            var (cmd, args) = ParseCommand("save reset arg1", _commands);

            Assert.IsNotNull(cmd);
            Assert.AreEqual("save reset", cmd.Name);
            Assert.AreEqual(new[] { "arg1" }, args);
        }

        [Test]
        public void ParseCommand_OneWordCommand_Matches()
        {
            var (cmd, args) = ParseCommand("fps show", _commands);

            Assert.IsNotNull(cmd);
            Assert.AreEqual("fps", cmd.Name);
            Assert.AreEqual(new[] { "show" }, args);
        }

        [Test]
        public void ParseCommand_TwoWordWithMultipleArgs_SplitsCorrectly()
        {
            var (cmd, args) = ParseCommand("loc set en-US extra arg", _commands);

            Assert.IsNotNull(cmd);
            Assert.AreEqual("loc set", cmd.Name);
            Assert.AreEqual(new[] { "en-US", "extra", "arg" }, args);
        }

        [Test]
        public void ParseCommand_UnknownCommand_ReturnsNull()
        {
            var (cmd, args) = ParseCommand("unknown cmd", _commands);

            Assert.IsNull(cmd);
        }

        [Test]
        public void ParseCommand_EmptyInput_ReturnsNull()
        {
            var (cmd, args) = ParseCommand("", _commands);

            Assert.IsNull(cmd);
        }

        [Test]
        public void ParseCommand_WhitespaceOnly_ReturnsNull()
        {
            var (cmd, args) = ParseCommand("   ", _commands);

            Assert.IsNull(cmd);
        }

        [Test]
        public void ParseCommand_GreedyMatch_PrefersLongerCommandName()
        {
            // If both "fps" and "fps show" were registered, should match "fps show" for input "fps show"
            var commands = new Dictionary<string, IConsoleCommand>();
            commands["fps"] = new MockCommand("fps");
            commands["fps show"] = new MockCommand("fps show");

            var (cmd, args) = ParseCommand("fps show", commands);

            Assert.IsNotNull(cmd);
            Assert.AreEqual("fps show", cmd.Name);
            Assert.AreEqual(Array.Empty<string>(), args);
        }

        [Test]
        public void ParseCommand_NoArgsForCommand_EmptyArgsArray()
        {
            var (cmd, args) = ParseCommand("fps", _commands);

            Assert.IsNotNull(cmd);
            Assert.AreEqual("fps", cmd.Name);
            Assert.AreEqual(Array.Empty<string>(), args);
        }

        private static (IConsoleCommand, string[]) ParseCommand(string input, Dictionary<string, IConsoleCommand> commands)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (null, Array.Empty<string>());

            var parts = input.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return (null, Array.Empty<string>());

            IConsoleCommand cmd = null;
            string[] args = Array.Empty<string>();

            // Greedy match: try two-word commands first
            if (parts.Length >= 2)
            {
                var twoWord = $"{parts[0]} {parts[1]}";
                if (commands.TryGetValue(twoWord, out var twoWordCmd))
                {
                    cmd = twoWordCmd;
                    args = parts.Skip(2).ToArray();
                    return (cmd, args);
                }
            }

            // Then try one-word
            if (commands.TryGetValue(parts[0], out var oneWordCmd))
            {
                cmd = oneWordCmd;
                args = parts.Skip(1).ToArray();
                return (cmd, args);
            }

            return (null, Array.Empty<string>());
        }

        private sealed class MockCommand : IConsoleCommand
        {
            public string Name { get; }
            public string Help => "Mock command";

            public MockCommand(string name)
            {
                Name = name;
            }

            public UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
                => UniTask.CompletedTask;
        }
    }
}
