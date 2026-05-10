using System;

namespace Zero.DevTools
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Help { get; }

        public ConsoleCommandAttribute(string name, string help = "")
        {
            Name = name;
            Help = help;
        }
    }
}
