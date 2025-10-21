// Admin Portal JavaScript
document.addEventListener('DOMContentLoaded', function () {
    // Toggle sidebar function
    window.toggleSidebar = function () {
        var sidebar = document.getElementById('sidebar');
        sidebar.classList.toggle('active');

        // Add overlay for mobile
        if (window.innerWidth <= 768) {
            var overlay = document.getElementById('sidebarOverlay');
            if (!overlay) {
                overlay = document.createElement('div');
                overlay.id = 'sidebarOverlay';
                overlay.className = 'sidebar-overlay';
                overlay.onclick = toggleSidebar;
                document.body.appendChild(overlay);
            }
            overlay.classList.toggle('active');
        }
    }

    // Close sidebar when clicking outside on mobile
    document.addEventListener('click', function (event) {
        var sidebar = document.getElementById('sidebar');
        var toggleButton = document.querySelector('.navbar-toggler');

        if (window.innerWidth <= 768 &&
            sidebar.classList.contains('active') &&
            !sidebar.contains(event.target) &&
            !toggleButton.contains(event.target)) {
            toggleSidebar();
        }
    });

    // Highlight active sidebar link
    function setActiveSidebarLink() {
        const activePage = window.location.pathname;
        const navLinks = document.querySelectorAll('#dashboardSidebar .nav-link');

        navLinks.forEach(link => {
            // Remove active class from all links
            link.classList.remove('active');

            // Check if the current page matches the link's href
            if (link.getAttribute('href') && activePage.includes(link.getAttribute('href'))) {
                link.classList.add('active');
            }
        });

        // Fallback for exact matches
        navLinks.forEach(link => {
            if (link.getAttribute('href') === activePage) {
                link.classList.add('active');
            }
        });
    }

    // Initialize active link highlighting
    setActiveSidebarLink();

    // Handle window resize
    window.addEventListener('resize', function () {
        if (window.innerWidth > 768) {
            var sidebar = document.getElementById('sidebar');
            var overlay = document.getElementById('sidebarOverlay');

            if (sidebar) sidebar.classList.remove('active');
            if (overlay) overlay.classList.remove('active');
        }
    });
});