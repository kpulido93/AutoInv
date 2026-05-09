using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Mvc.RazorPages;

using Services;

namespace Pages
{
    public class IndexModel : PageModel
    {
        private readonly EventStore _eventStore;

        public IndexModel(EventStore eventStore) => _eventStore = eventStore;

        public List<string> Events { get; private set; } = new();
        public string CspNonce { get; private set; } = "";

        public void OnGet()
        {
            CspNonce = HttpContext.Items["CspNonce"]?.ToString() ?? "";
        }
    }
}
