using System;
using SpringExpressions.Parser.antlr;
using SpringExpressions.Parser.antlr.collections;

namespace SpringExpressions
{
    /// <summary>
    /// For internal purposes only. Use <see cref="BaseNode"/> for expression node implementations.
    /// </summary>
    /// <remarks>
    /// The class originally existed to make parsed expressions serializable, because antlr.CommonAST is not
    /// marked as [Serializable]. Expressions are no longer serializable at all, so what it carries now is
    /// the node's text and token type, the <see cref="IsNullConditional"/> flag, and the node factory the
    /// parser builds nodes through.
    /// </remarks>
    public class SpringAST : Parser.antlr.BaseAST
    {
        #region Global SpringAST Factory

        internal class SpringASTCreator : Parser.antlr.ASTNodeCreator
        {
            public override Parser.antlr.collections.AST Create()
            {
                return new SpringAST();
            }

            public override string ASTNodeTypeName
            {
                get { return typeof(SpringAST).FullName; }
            }
        }

        /// <summary>
        /// The global SpringAST node factory
        /// </summary>
        internal static readonly SpringASTCreator Creator = new SpringASTCreator();

        #endregion

        #region Members

        private string text;
        private int ttype;

        #endregion

        /// <summary>
        /// Create an instance
        /// </summary>
        public SpringAST()
        {}

        /// <summary>
        /// Create an instance from a token
        /// </summary>
        public SpringAST(IToken token)
        {
            initialize(token);
        }

        /// <summary>
        /// initialize this instance from an AST
        /// </summary>
        public override void initialize(AST t)
        {
            this.setText(t.getText());
            this.Type = t.Type;
        }

        /// <summary>
        /// initialize this instance from an IToken
        /// </summary>
        public override void initialize(IToken tok)
        {
            this.setText(tok.getText());
            this.Type = tok.Type;
        }

        /// <summary>
        /// initialize this instance from a token type number and a text
        /// </summary>
        public override void initialize(int t, string txt)
        {
            this.Type = t;
            this.setText(txt);
        }

        /// <summary>
        /// gets or sets the token type of this node
        /// </summary>
        public override int Type
        {
            get { return this.ttype; }
            set { this.ttype = value; }
        }

        /// <summary>
        /// gets or sets the text of this node
        /// </summary>
        public string Text
        {
            get { return this.getText(); }
            set { this.setText(value); }
        }

        /// <summary>
        /// True when this node is reached through a null-conditional operator (<c>?.</c> or <c>?[</c>).
        /// </summary>
        /// <remarks>
        /// The flag means "null-check the context flowing into me": if that context is null, this node is
        /// not evaluated and the remainder of the access chain is abandoned, the whole chain yielding null.
        /// Set by the parser on the node following <c>?.</c>, or on an indexer opened with <c>?[</c>.
        /// </remarks>
        public bool IsNullConditional { get; set; }

        /// <summary>
        /// sets the text of this node
        /// </summary>
        public override void setText(string txt)
        {
            this.text = txt;
        }

        /// <summary>
        /// gets the text of this node
        /// </summary>
        public override string getText()
        {
            return this.text;
        }

    }
}