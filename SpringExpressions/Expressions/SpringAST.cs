using System;
using System.Runtime.Serialization;
using SpringExpressions.Parser.antlr;
using SpringExpressions.Parser.antlr.collections;

namespace SpringExpressions
{
    /// <summary>
    /// For internal purposes only. Use <see cref="BaseNode"/> for expression node implementations.
    /// </summary>
    /// <remarks>
    /// This class is only required to enable serialization of parsed Spring expressions since antlr.CommonAST
    /// unfortunately is not marked as [Serializable].<br/>
    /// <br/>
    /// <b>Note:</b>Since SpringAST implements <see cref="ISerializable"/>, deriving classes 
    /// have to explicitely override <see cref="GetObjectData"/> if they need to persist additional
    /// data during serialization.
    /// </remarks>
    [Serializable]
    public class SpringAST : Parser.antlr.BaseAST, ISerializable
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

        #region ISerializable Implementation

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected SpringAST(SerializationInfo info, StreamingContext context)
        {
            base.down = (BaseAST)info.GetValue("down", typeof(BaseAST));
            base.right = (BaseAST)info.GetValue("right", typeof(BaseAST));
            this.ttype = info.GetInt32("ttype");
            this.text = info.GetString("text");
            this.IsNullConditional = TryGetBoolean(info, IsNullConditionalKey);
        }

        /// <summary>
        /// populate SerializationInfo from this instance
        /// </summary>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("down", base.down, typeof(SpringAST));
            info.AddValue("right", base.right, typeof(SpringAST));
            info.AddValue("ttype", this.Type, typeof(int));
            info.AddValue("text", this.Text, typeof(string));

            // Written only when set, so that a node without a null-conditional operator produces exactly
            // the same four values it always has. Streams written by builds that predate this member stay
            // readable, and streams written now stay readable by those builds.
            if (this.IsNullConditional)
                info.AddValue(IsNullConditionalKey, true);
        }

        private const string IsNullConditionalKey = "isNullConditional";

        /// <summary>
        /// Reads an optional boolean, returning <paramref name="defaultValue"/> when the member is absent.
        /// </summary>
        /// <remarks>
        /// <see cref="SerializationInfo"/> offers no "try get": <c>GetBoolean</c> throws
        /// <see cref="SerializationException"/> when the member was never written, which is the case for
        /// every stream produced before the member existed. Enumerating the entries cannot throw.
        /// </remarks>
        private static bool TryGetBoolean(SerializationInfo info, string name, bool defaultValue = false)
        {
            foreach (SerializationEntry entry in info)
            {
                if (entry.Name == name)
                    return entry.Value is bool value ? value : defaultValue;
            }

            return defaultValue;
        }
        
        #endregion
    }
}