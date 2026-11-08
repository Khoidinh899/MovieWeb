// Trang danh sách phim V2: popup quảng cáo theo placement (logic V1 trong các view Movie/*, Category, Country).
// Placement lấy từ data-placement của chính thẻ <script> này, ví dụ Popup_PhimLe.
(function () {
    var self = document.currentScript;
    var placement = self && self.getAttribute('data-placement');
    if (!placement) return;

    function createAdModalHtml(banner, modalId) {
        var closeButtonCss = 'position:absolute;top:-15px;right:-10px;background-color:white;border-radius:50%;' +
            'opacity:1;box-shadow:0 0 10px rgba(0,0,0,0.5);width:30px;height:30px;padding:0;';
        return '<div class="modal fade" id="' + modalId + '" tabindex="-1" aria-hidden="true">' +
            '<div class="modal-dialog modal-dialog-centered modal-lg"><div class="modal-content" style="background-color:transparent;border:none;">' +
            '<div class="modal-body" style="padding:0;position:relative;">' +
            '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close" style="' + closeButtonCss + '"></button>' +
            '<a href="' + banner.clickUrl + '" target="_blank" rel="noopener noreferrer">' +
            '<img src="' + banner.adContentUrl + '" class="img-fluid rounded" alt="' + banner.adName + '"></a>' +
            '</div></div></div></div>';
    }

    async function fetchPopupAd() {
        try {
            var response = await fetch('/api/ads/get-placements?placements=' + encodeURIComponent(placement));
            if (!response.ok) {
                console.error('Lỗi API (' + placement + '):', response.statusText);
                return;
            }
            var banners = await response.json();
            if (!banners || banners.length === 0) return;

            var modalId = 'adModal_' + placement;
            document.body.insertAdjacentHTML('beforeend', createAdModalHtml(banners[0], modalId));

            var adModalElement = document.getElementById(modalId);
            if (adModalElement && window.bootstrap) {
                var adModal = new window.bootstrap.Modal(adModalElement);
                adModal.show();
                adModalElement.addEventListener('hidden.bs.modal', function () {
                    adModalElement.remove();
                });
            }
        } catch (error) {
            console.error('Lỗi khi tải popup ad (' + placement + '):', error);
        }
    }

    document.addEventListener('DOMContentLoaded', fetchPopupAd);
})();
