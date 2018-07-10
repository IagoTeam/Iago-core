using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NFluent;
using NFluent.Extensibility;
using Xunit;

namespace Bolt.Tests
{
    public static class ProjectHelperCheckExtensions
    {
        public static ICheckLink<ICheck<BuildContext>> HasNoErrors(this ICheck<BuildContext> check)
        { 
            var actual = ExtensibilityHelper.ExtractChecker(check).Value;
            
            ExtensibilityHelper.BeginCheck(check)
                .FailWhen(sut => sut.HasError, $"Current context has errors whereas it should not : {actual.Error?.Message}")
                .OnNegate("hum, I don't see any errors whereas it's supposed to have...")
                .EndCheck();
            return ExtensibilityHelper.BuildCheckLink(check);
        }
    }
    
    public class ProjectHelperTests
    {
        /// <summary>
        /// end to end : I want a helper that is able to give me an assembly
        /// that I can work with from a project folder containing a .csp
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task get_access_to_resulting_assembly_on_a_project_folder()
        {
            var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder());

            var resultContext = await projectHelper.BuildProjectAssembly();

            var asm = resultContext.ResultingAssembly;
            var greetType = asm.GetType("Greetings");
            Check.That(greetType).IsNotNull();
            
            var helloProperty = greetType.GetProperty("Hello");
            Check.That(helloProperty).IsNotNull();

            var greet = Activator.CreateInstance(greetType);
            Check.That(greet).IsNotNull();

            var hello = helloProperty.GetValue(greet);
            Check.That(hello).IsEqualTo("Hello World");
        }
    }


    public class BuildingContext
    {
        public class with_projectFileChooser
        {
        
            [Fact]
            public async Task ensure_working_directoty_is_set()
            {
            
                var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder());
                
                Check.That(projectHelper.WorkingDirectory).IsEqualTo(SampleProjectHelper.WorkFolder());
            }
            
            [Fact]
            public async Task ensure_projectFileChooser_is_called_in_build()
            {
            
                Action<ConventionsSetter> setconventions =  overload =>
                {
                    overload.SetProjectFileChooser(new FakeProjectFileChooser());
                };
                var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder(), setconventions);
                var resultContext = await projectHelper.BuildProjectAssembly();

                Check.That(resultContext.ProjectFile).IsEqualTo("/path/to/csproj");
            }
            
            [Fact]
            public async Task ensure_default_projectFileChooser_take_the_right_File()
            {
                var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder());
                var resultContext = await projectHelper.BuildProjectAssembly();

                Check.That(resultContext).HasNoErrors();
                Check.That(resultContext.ProjectFile).IsEqualTo(Path.Combine(SampleProjectHelper.WorkFolder(), "Sample.csproj"));
                Check.That(resultContext.ProjectName).IsEqualTo("Sample");
            }
        }

        public class with_projectBuilder
        {
            [Fact]
            public async Task
                ensure_projectBuilder_is_called()
            {
                Action<ConventionsSetter> setconventions = overload =>
                    {
                        overload.SetProjectBuilder(new FakeProjectBuilder());
                    };
                var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder(), setconventions);
                var resultContext = await projectHelper.BuildProjectAssembly();

                Check.That(resultContext.ResultingAssemblyFile).IsEqualTo("/path/to/resulting/dll");
                Check.That(resultContext.BinFolder).IsEqualTo("/path/to/resulting/dll/bin/folder");

            }
            
            [Fact]
            public async Task
                ensure_default_project_builder_builds_the_project_when_it_exists()
            {
                var projectHelper = new ProjectHelper(SampleProjectHelper.WorkFolder());
                var resultContext = await projectHelper.BuildProjectAssembly();

                var  expectedBuildFile=
                    Path.Combine(SampleProjectHelper.WorkFolder(), "bin/Debug/netstandard2.0/Sample.dll");
                var expectedBinFolder =
                    Path.Combine(SampleProjectHelper.WorkFolder(), "bin/Debug/netstandard2.0");

                Check.That(resultContext.ResultingAssemblyFile).IsEqualTo(expectedBuildFile);
                Check.That(resultContext.BinFolder).IsEqualTo(expectedBinFolder);
                Check.That(resultContext.AssemblyLength).IsGreaterThan(0);

            }
        }
            
        
    }
    

    public class FakeProjectFileChooser : IProjectFileChooser
    {
        public BuildContext FindProjectFile(BuildContext context)
        {
            context.ProjectFile = "/path/to/csproj";
            return context;
        }
    }
    
    public class FakeProjectBuilder: IProjectBuilder
    {
        public BuildContext BuildProject(BuildContext context)
        {
            context.ResultingAssemblyFile = "/path/to/resulting/dll";
            context.BinFolder = "/path/to/resulting/dll/bin/folder";
            return context;
        }
    }

    

/* EXPERIMENT WITH FILEINFO :
 ---------------------------- 
    public class InMemoryFileProvider : IFileProvider
    {
        private readonly string _root;
        private readonly IList<(string name, string path, string content)>  source;
        private readonly IEnumerable<IFileInfo> list;
        
        public InMemoryFileProvider(string root, IList<(string name, string path, string content)> contents)
        {
            _root = root;
            source = contents;
            list = contents.Select(x => new InMemoryFileInfo(x.content, x.path, x.name));
        }

        public IFileInfo GetFileInfo(string subpath)
            => list.FirstOrDefault(x => x.PhysicalPath == Path.Combine(_root,subpath));

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            throw new NotImplementedException();
        }

        public IChangeToken Watch(string filter)
        {
            throw new NotImplementedException();
        }
    }

    public class InMemoryFileInfo : IFileInfo
    {
        private readonly string _content;

        public InMemoryFileInfo(string content, string physicalPath,
            string name = null,
            DateTimeOffset lastModified = default(DateTimeOffset), bool exists = true)
        {
            _content = content;
            PhysicalPath = physicalPath;
            Name = name ?? physicalPath.Split('/', '\\').Last();
            Exists = exists;
            LastModified = lastModified == default(DateTimeOffset) ? DateTimeOffset.Now : lastModified;
            IsDirectory = false;
        }
        public Stream CreateReadStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(_content));
        }

        public bool Exists { get; }
        public long Length { get; }
        public string PhysicalPath { get; }
        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public bool IsDirectory { get; }
    }

    public class InMemoryDirectoryContents : IDirectoryContents
    {
        private readonly IEnumerable<IFileInfo> _list;

        public InMemoryDirectoryContents(IEnumerable<IFileInfo> list = null)
        {
            _list = list;
            Exists = list != null;
        }

        public IEnumerator<IFileInfo> GetEnumerator()
            => _list?.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _list?.GetEnumerator();

        public bool Exists { get; }
    }
    */
}
