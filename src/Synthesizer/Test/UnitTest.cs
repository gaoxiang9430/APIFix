using Xunit;
using ZSS;

namespace CSharpEngine.Tests
{
    public class Test
    {
        [Fact]
        public void TestZhangShaSha()
        {
            SimpleNode A = new SimpleNode("f")
                    .AddChild(new SimpleNode("d")
                        .AddChild(new SimpleNode("a"))
                        .AddChild(new SimpleNode("c")
                            .AddChild(new SimpleNode("b"))
                        )
                    ).AddChild(new SimpleNode("e"));

            SimpleNode B = new SimpleNode("f")
                    .AddChild(new SimpleNode("c")
                        .AddChild(new SimpleNode("d")
                            .AddChild(new SimpleNode("a"))
                            .AddChild(new SimpleNode("b"))
                        )
                    ).AddChild(new SimpleNode("e"));

            var shasha = new ZhangShaSha<SimpleNode>(A, B);
            Assert.Equal(2, shasha.simple_distance());
            var ops = shasha.simple_edit();
            foreach (var op in ops)
            {
                Utils.LogTest("*******************************************" + op.ToString());
            }
        }
    }
}
