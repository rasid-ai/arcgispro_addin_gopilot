using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;

namespace Rasid.Services.Geometry
{
    internal static class FrameworkApplicationHelpers
    {
        public static Task ActivateToolAsync(string toolId)
        {
            return FrameworkApplication.SetCurrentToolAsync(toolId);
        }

        public static Task DeactivateToExploreAsync()
        {
            return FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
        }
    }
}
