// 
// Authors
//    Sebastien Pouliot  <sebastien@xamarin.com>
//
// Copyright 2013 Xamarin Inc. http://www.xamarin.com
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Xml.Linq;

namespace Xamarin.ApiDiff {

	public class AssemblyComparer : Comparer {

		XDocument source;
		XDocument target;
		NamespaceComparer comparer;

		public AssemblyComparer (string sourceFile, string targetFile)
		{
			source = XDocument.Load (sourceFile);
			target = XDocument.Load (targetFile);
			comparer =  new NamespaceComparer ();
		}

		public void Compare ()
		{
			Compare (source.Element ("assemblies").Elements ("assembly"), 
			         target.Element ("assemblies").Elements ("assembly"));
			Output.Flush ();
		}

		public override void SetContext (XElement current)
		{
			State.Assembly = current.Attribute ("name").Value;
		}

		public override void Added (XElement target)
		{
			// one assembly per xml file
		}

		public override void Modified (XElement source, XElement target)
		{
			Output.WriteLine ("<h1>{0}.dll</h1>", source.Attribute ("name").Value);
			// TODO: version
			// ? custom attributes ?
			comparer.Compare (source, target);
		}

		public override void Removed (XElement source)
		{
			// one assembly per xml file
		}
	}
}