// Automatically scroll to the active aside menu item.
const mainMenu = document.getElementById('fsdocs-main-menu');
function scrollToActiveItem(activeItem) {
    const halfMainMenuHeight = mainMenu.offsetHeight / 2
    if(activeItem.offsetTop > halfMainMenuHeight){
        mainMenu.scrollTop = (activeItem.offsetTop - halfMainMenuHeight) - (activeItem.offsetHeight / 2);
    }
}

const activeItem = document.querySelector("aside .nav-item.active");
if (activeItem && mainMenu) {
    scrollToActiveItem(activeItem);
}

if(location.hash) {
    const header = document.querySelector(`a[href='${location.hash}']`);
    header.scrollIntoView({ behavior: 'instant'});
}

if(location.pathname.startsWith('/reference/')) {
    // Scroll to API Reference header
    const navHeaders = document.querySelectorAll(".nav-header");
    for (const navHeader of navHeaders) {
        if (navHeader.textContent && navHeader.textContent.trim() === 'API Reference') {
            scrollToActiveItem(navHeader);
        }
    }
}