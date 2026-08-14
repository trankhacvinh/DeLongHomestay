import PhotoSwipeLightbox from 'https://cdn.jsdelivr.net/npm/photoswipe@5.4.4/dist/photoswipe-lightbox.esm.js';

const gallery = document.getElementById('room-gallery');
if (gallery) {
    const lightbox = new PhotoSwipeLightbox({
        gallery: '#room-gallery',
        children: 'a',
        pswpModule: () => import('https://cdn.jsdelivr.net/npm/photoswipe@5.4.4/dist/photoswipe.esm.js'),
        bgOpacity: 0.94,
        showHideAnimationType: 'zoom'
    });
    lightbox.init();
}
