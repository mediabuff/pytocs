﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pytocs.CodeModel
{
    public class CodeGenerator
    {
        private CSharpCodeProvider provider;
        private CodeCompileUnit unt;

        public CodeGenerator(CodeCompileUnit unt, string modulePath, string moduleName)
        {
            this.unt = unt;
            this.provider = new CSharpCodeProvider();
            this.Scope = new List<CodeStatement>();  // dummy scope.
            this.CurrentNamespace = new CodeNamespace(modulePath);
            this.CurrentType = new CodeTypeDeclaration(moduleName)
            {
                IsClass = true,
                Attributes = MemberAttributes.Static | MemberAttributes.Public
            };
            CurrentNamespace.Types.Add(CurrentType);
            unt.Namespaces.Add(CurrentNamespace);
        }

        public List<CodeStatement> Scope { get;  private set; }
        public CodeMemberMethod CurrentMethod { get; set; }
        public CodeNamespace CurrentNamespace { get; set; }
        public CodeTypeDeclaration CurrentType { get; set; }


        internal CodeExpression Access(CodeExpression exp, string fieldName)
        {
            return new CodeFieldReferenceExpression(exp, fieldName);
        }

        public CodeBinaryOperatorExpression BinOp(CodeExpression l, CodeOperatorType op, CodeExpression r)
        {
            return new CodeBinaryOperatorExpression(l, op, r);
        }

        public CodeCatchClause CatchClause(string localName, CodeTypeReference type, Action generateClauseBody)
        {
            var clause = new CodeCatchClause(localName, type);
            var oldScope = Scope;
            Scope = clause.Statements;
            generateClauseBody();
            Scope = oldScope;
            return clause;
        }

        public CodeTypeDeclaration Class(string name, IEnumerable<string> baseClasses, Action body)
        {
            var c = new CodeTypeDeclaration
            {
                IsClass = true,
                Name = name,
            };
            CurrentType.Members.Add(c);
            c.BaseTypes.AddRange(baseClasses.Select(b => new CodeTypeReference(b)).ToArray());
            var old = CurrentType;
            var oldMethod = CurrentMethod;
            CurrentType = c;
            CurrentMethod = null;
            body();
            CurrentMethod = oldMethod;
            CurrentType = old;
            return c;
        }

        public CodeAssignStatement Assign(CodeExpression lhs, CodeExpression rhs)
        {
            var ass = new CodeAssignStatement(lhs, rhs);
            Scope.Add(ass);
            return ass;
        }

        internal CodeConditionStatement If(CodeExpression test, Action xlatThen, Action xlatElse)
        {
            var i = new CodeConditionStatement
            {
                Condition = test
            };
            Scope.Add(i);
            var old = Scope;
            Scope = i.TrueStatements;
            xlatThen();
            Scope = i.FalseStatements;
            xlatElse();
            Scope = old;
            return i;
        }

        public CodeStatement Foreach(CodeExpression exp, CodeExpression list, Action xlatLoopBody)
        {
            var c = new CodeForeachStatement(exp, list);
            Scope.Add(c);
            var old = Scope;
            Scope = c.Statements;
            xlatLoopBody();
            Scope = old;
            return c;
        }

        public CodeExpression Appl(CodeExpression fn, params CodeExpression [] args)
        {
            return new CodeApplicationExpression(fn, args);
        }

        public CodeStatement SideEffect(CodeExpression exp)
        {
            var sideeffect = new CodeExpressionStatement(exp);
            Scope.Add(sideeffect);
            return sideeffect;
        }

        internal CodeConstructor Constructor(IEnumerable<CodeParameterDeclarationExpression> parms, Action body)
        {
            var cons = new CodeConstructor
            {
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };
            cons.Parameters.AddRange(parms.ToArray());
            CurrentType.Members.Add(cons);

            GenerateMethodBody(cons, body);
            return cons;
        }

        internal CodeMemberMethod Method(string name, IEnumerable<CodeParameterDeclarationExpression> parms, Action body)
        {
            var method = new CodeMemberMethod
            {
                Name = name,
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(object))
            };
            method.Parameters.AddRange(parms.ToArray());
            CurrentType.Members.Add(method);

            GenerateMethodBody(method, body);
            return method;
        }

        internal CodeMemberMethod StaticMethod(string name, IEnumerable<CodeParameterDeclarationExpression> parms, Action body)
        {
            var method = new CodeMemberMethod
            {
                Name = name,
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(typeof(object))
            };
            method.Parameters.AddRange(parms.ToArray());
            CurrentType.Members.Add(method);

            GenerateMethodBody(method, body);
            return method;
        }

        private void GenerateMethodBody(CodeMemberMethod method, Action body)
        {
            var old = Scope;
            var oldMethod = CurrentMethod;
            CurrentMethod = method;
            Scope = method.Statements;
            body();
            Scope = old;
            CurrentMethod = oldMethod;
        }

        internal CodeParameterDeclarationExpression Param(Type type, string name)
        {
            return new CodeParameterDeclarationExpression(type, name);
        }

        internal CodeParameterDeclarationExpression Param(Type type, string name, CodeExpression defaultValue)
        {
            return new CodeParameterDeclarationExpression(type, name, defaultValue);
        }

        internal void Return(CodeExpression e = null)
        {
            Scope.Add(new CodeMethodReturnStatement(e));
        }

        public void Using(string @namespace)
        {
            CurrentNamespace.Imports.Add(new CodeNamespaceImport(@namespace));
        }

        public void Using(string alias, string @namespace)
        {
            CurrentNamespace.Imports.Add(new CodeNamespaceImport(
                EscapeKeywordName(alias) +
                " = " +
                EscapeKeywordName(@namespace)));
        }

        public string EscapeKeywordName(string name)
        {
            return IndentingTextWriter.NameNeedsQuoting(name)
                ? "@" + name
                : name;
        }

        public CodeMemberField Field(string fieldName)
        {
            var field = new CodeMemberField(typeof(object), fieldName)
            {
                Attributes = MemberAttributes.Public,
            };
            CurrentType.Members.Add(field);
            return field;
        }

        internal CodeMemberField Field(string fieldName, CodeExpression initializer)
        {
            var field = new CodeMemberField(typeof(object), fieldName)
            {
                Attributes = MemberAttributes.Public,
                InitExpression = initializer,
            };
            CurrentType.Members.Add(field);
            return field;
        }

        internal CodeArrayIndexerExpression Aref(CodeExpression exp, CodeExpression[] indices)
        {
            return new CodeArrayIndexerExpression(exp, indices);
        }

        internal CodeThrowExceptionStatement Throw(CodeExpression codeExpression)
        {
            var t = new CodeThrowExceptionStatement(codeExpression);
            Scope.Add(t);
            return t;
        }

        internal CodeThrowExceptionStatement Throw()
        {
            var t = new CodeThrowExceptionStatement();
            Scope.Add(t);
            return t;
        }

        internal CodeExpression BinOp(CodeExpression l, Func<object, object, bool> func, CodeExpression r)
        {
            throw new NotImplementedException();
        }

        internal CodeExpression Lambda(CodeExpression[] args, CodeExpression expr)
        {
            return new CodeLambdaExpression(args, expr);
        }

        public CodeExpression ListInitializer(IEnumerable<CodeExpression> exprs)
        {
            var list = new CodeObjectCreateExpression
            {
                Type = new CodeTypeReference("List", new CodeTypeReference("object"))
            };
            EnsureImport("System.Collections.Generic");
            list.Initializers.AddRange(exprs);
            return list;
        }

        public void EnsureImport(string nmespace)
        {
            if (CurrentNamespace.Imports.Where(i => i.Namespace == nmespace).Any())
                return;
            CurrentNamespace.Imports.Add(new CodeNamespaceImport(nmespace));
        }

        public CodeTryCatchFinallyStatement Try(
            Action genTryStatements,
            IEnumerable<CodeCatchClause> catchClauses,
            Action genFinallyStatements)
        {
            var t = new CodeTryCatchFinallyStatement();
            var oldScope = Scope;
            Scope = t.TryStatements;
            genTryStatements();
            t.CatchClauses.AddRange(catchClauses);
            Scope = t.FinallyStatements;
            genFinallyStatements();
            Scope = oldScope;
            Scope.Add(t);
            return t;
        }

        internal void Break()
        {
            Scope.Add(new CodeBreakStatement());
        }

        public void Continue()
        {
            Scope.Add(new CodeContinueStatement());
        }

        public CodePreTestLoopStatement While(
            CodeExpression exp,
            Action generateBody)
        {
            var w = new CodePreTestLoopStatement
            {
                Test = exp,
            };
            var oldScope = Scope;
            Scope = w.Body;
            generateBody();
            Scope = oldScope;
            Scope.Add(w);
            return w;
        }

        public CodePostTestLoopStatement DoWhile(
            Action generateBody,
            CodeExpression exp)
        {
            var dw = new CodePostTestLoopStatement
            {
                Test = exp,
            };
            Scope.Add(dw);
            var oldScope = Scope;
            Scope = dw.Body;
            generateBody();
            Scope = oldScope;
            return dw;
        }

        internal CodeYieldStatement Yield(CodeExpression exp)
        {
            var y = new CodeYieldStatement(exp);
            Scope.Add(y);
            return y;
        }

        internal CodeTypeReference TypeRef(string typeName)
        {
            return new CodeTypeReference(typeName);
        }

        internal CodeAttributeDeclaration CustomAttr(CodeTypeReference typeRef, params CodeAttributeArgument [] args)
        {
            return new CodeAttributeDeclaration
            {
                AttributeType = typeRef,
                Arguments = args.ToList(),
            };
            throw new NotImplementedException();
        }

        internal CodeCommentStatement Comment(string comment)
        {
            var c = new CodeCommentStatement(comment);
            Scope.Add(c);
            return c;
        }

        public CodeExpression TypeRefExpr(string typeName)
        {
            return new CodeTypeReferenceExpression(typeName);
        }

        internal CodeUsingStatement Using(
            IEnumerable<CodeStatement> initializers,
            Action xlatUsingBody)
        {
            var u = new CodeUsingStatement();
            Scope.Add(u);
            u.Initializers.AddRange(initializers);
            var old = Scope;
            Scope = u.Statements;
            xlatUsingBody();
            Scope = old;
            return u;
        }
    }
}