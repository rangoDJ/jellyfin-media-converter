// Media Converter - item detail page quick-convert button.
//
// Jellyfin's plugin config page loader strips <script> tags from the HTML it fetches
// for a plugin's own config page (confirmed on server 12.0-rc5), so this script can't be
// loaded through the plugin's normal mechanism. Instead, paste this into a global script
// injector (e.g. the "JavaScript Injector" plugin), which loads it via a real <script> tag
// in the top-level page - those always execute normally, regardless of the config-page issue.
//
// It watches for navigation to a movie/episode detail page and shows a floating "Convert"
// button that calls this plugin's existing MediaConverter/Convert API.
(function () {
    if (window.__mediaConverterItemButtonInstalled) {
        return;
    }

    window.__mediaConverterItemButtonInstalled = true;

    var currentItemId = null;
    var buttonEl = null;

    function getItemIdFromHash() {
        var match = /[#&?]id=([0-9a-fA-F-]{32,36})/.exec(location.hash);
        return match ? match[1] : null;
    }

    function ensureButton() {
        if (buttonEl) {
            return buttonEl;
        }

        buttonEl = document.createElement('button');
        buttonEl.textContent = 'Convert';
        buttonEl.style.position = 'fixed';
        buttonEl.style.bottom = '20px';
        buttonEl.style.right = '20px';
        buttonEl.style.zIndex = '9999';
        buttonEl.style.padding = '10px 18px';
        buttonEl.style.borderRadius = '4px';
        buttonEl.style.border = 'none';
        buttonEl.style.background = '#00a4dc';
        buttonEl.style.color = '#fff';
        buttonEl.style.fontWeight = 'bold';
        buttonEl.style.cursor = 'pointer';
        buttonEl.style.display = 'none';
        buttonEl.addEventListener('click', openDialog);
        document.body.appendChild(buttonEl);
        return buttonEl;
    }

    function refreshForCurrentItem() {
        var itemId = getItemIdFromHash();
        currentItemId = null;
        ensureButton().style.display = 'none';

        if (!itemId || !window.ApiClient) {
            return;
        }

        ApiClient.getItem(ApiClient.getCurrentUserId(), itemId).then(function (item) {
            if (item && (item.Type === 'Movie' || item.Type === 'Episode')) {
                currentItemId = item.Id;
                ensureButton().style.display = 'block';
            }
        }).catch(function () {
            // Not every hash change is an item detail page; ignore lookup failures.
        });
    }

    function closeDialog() {
        var container = document.getElementById('mcQuickConvert');
        if (container) {
            container.remove();
        }
    }

    function openDialog() {
        if (!currentItemId || document.getElementById('mcQuickConvert')) {
            return;
        }

        var container = document.createElement('div');
        container.id = 'mcQuickConvert';
        container.style.position = 'fixed';
        container.style.bottom = '70px';
        container.style.right = '20px';
        container.style.zIndex = '9999';
        container.style.background = '#101010';
        container.style.border = '1px solid #333';
        container.style.borderRadius = '4px';
        container.style.padding = '12px';
        container.style.color = '#fff';
        container.style.minWidth = '220px';
        container.innerHTML =
            '<div style="margin-bottom:8px;">Container ' +
            '<select id="mcContainer"><option value="mkv">mkv</option><option value="mp4">mp4</option></select></div>' +
            '<div style="margin-bottom:8px;">Codec ' +
            '<select id="mcCodec"><option value="hevc">HEVC</option><option value="h264">H.264</option><option value="av1">AV1</option></select></div>' +
            '<div style="margin-bottom:8px;">Mode ' +
            '<select id="mcMode"><option value="Variant">New variant</option><option value="Replace">Replace</option></select></div>' +
            '<button id="mcStart">Start</button> <button id="mcCancel">Cancel</button>' +
            '<div id="mcStatus" style="margin-top:8px;font-size:0.85em;"></div>';
        document.body.appendChild(container);

        document.getElementById('mcCancel').addEventListener('click', closeDialog);

        document.getElementById('mcStart').addEventListener('click', function () {
            var status = document.getElementById('mcStatus');
            status.textContent = 'Starting...';

            ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl('MediaConverter/Convert'),
                data: JSON.stringify({
                    ItemId: currentItemId,
                    Container: document.getElementById('mcContainer').value,
                    VideoCodec: document.getElementById('mcCodec').value,
                    Quality: 23,
                    Mode: document.getElementById('mcMode').value
                }),
                contentType: 'application/json',
                dataType: 'json'
            }).then(function () {
                status.textContent = 'Conversion started.';
                setTimeout(closeDialog, 1500);
            }).catch(function (error) {
                status.textContent = 'Failed: ' + ((error && (error.message || error.statusText)) || 'unknown error');
            });
        });
    }

    window.addEventListener('hashchange', refreshForCurrentItem);
    refreshForCurrentItem();
})();
