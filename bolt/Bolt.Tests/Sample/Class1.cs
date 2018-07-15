using System;

namespace Sample
{
    public class Greetings
    {
        public string Hello { get; } = "Hello World";

        public string ToHtml(string md)
        {
            return Markdig.Markdown.ToHtml(md);
        }
    }
}
