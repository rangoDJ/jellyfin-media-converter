(function () {
    var selectedItemIds = [];

    function apiGet(path) {
        return ApiClient.getJSON(ApiClient.getUrl(path));
    }

    function apiPost(path, body) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body || {}),
            contentType: 'application/json',
            dataType: 'json'
        });
    }

    function apiDelete(path) {
        return ApiClient.ajax({
            type: 'DELETE',
            url: ApiClient.getUrl(path)
        });
    }

    // For PUT endpoints that return 204 No Content (Rules/{id}) - not forcing dataType: 'json'
    // for the same reason apiPostNoContent doesn't (see below).
    function apiPut(path, body) {
        return ApiClient.ajax({
            type: 'PUT',
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body || {}),
            contentType: 'application/json'
        });
    }

    // For POST endpoints that return 204 No Content (Cancel, KeepVariant, KeepOriginal) - unlike
    // apiPost, this doesn't force dataType: 'json', which would otherwise try to parse the empty
    // body and fail with "Unexpected end of JSON input" even though the request succeeded.
    function apiPostNoContent(path) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl(path)
        });
    }

    function showError(context, error) {
        console.error('Media Converter: ' + context, error);
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            var detail = (error && (error.message || error.statusText)) || 'request failed';
            banner.textContent = context + ': ' + detail;
            banner.style.display = 'block';
            // The banner lives at the top of the page while actions like the Jobs bulk buttons
            // are further down, so without this a failure silently updates an off-screen element.
            banner.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    function clearError() {
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            banner.style.display = 'none';
        }
    }

    function formatBytes(bytes) {
        if (!bytes) {
            return '';
        }

        var gb = bytes / (1024 * 1024 * 1024);
        if (gb >= 1) {
            return gb.toFixed(2) + ' GB';
        }

        return (bytes / (1024 * 1024)).toFixed(0) + ' MB';
    }

    function formatMediaInfo(info) {
        if (!info) {
            return 'stats unavailable';
        }

        var parts = [];

        if (info.VideoCodec) {
            var resolution = (info.Width && info.Height) ? (' ' + info.Width + 'x' + info.Height) : '';
            var videoBitrate = formatBitrate(info.VideoBitRate || info.OverallBitRate);
            parts.push(info.VideoCodec.toUpperCase() + resolution + (videoBitrate ? (' @ ' + videoBitrate) : ''));
        }

        if (info.AudioCodec) {
            var channels = info.AudioChannels ? (' ' + info.AudioChannels + 'ch') : '';
            var audioBitrate = formatBitrate(info.AudioBitRate);
            parts.push(info.AudioCodec.toUpperCase() + channels + (audioBitrate ? (' @ ' + audioBitrate) : ''));
        }

        parts.push(info.SubtitleTracks && info.SubtitleTracks.length
            ? 'Subs: ' + info.SubtitleTracks.length
            : 'No subs');

        var size = formatBytes(info.FileSizeBytes);
        if (size) {
            parts.push(size);
        }

        return parts.length ? parts.join(' · ') : 'stats unavailable';
    }

    function loadMediaInfo(itemId, span) {
        span.textContent = 'Loading stats…';
        apiGet('MediaConverter/Items/' + itemId + '/MediaInfo')
            .then(function (info) {
                span.textContent = formatMediaInfo(info);
            })
            .catch(function () {
                span.textContent = '';
            });
    }

    function appendStatsSpan(row, itemId) {
        var stats = document.createElement('span');
        stats.className = 'mcStats';
        stats.style.marginLeft = '8px';
        stats.style.opacity = '0.7';
        row.appendChild(stats);
        loadMediaInfo(itemId, stats);
    }

    function formatBitrate(bitsPerSecond) {
        if (!bitsPerSecond) {
            return null;
        }

        if (bitsPerSecond >= 1000000) {
            return (bitsPerSecond / 1000000).toFixed(2) + ' Mbps';
        }

        return (bitsPerSecond / 1000).toFixed(0) + ' kbps';
    }

    function renderMediaInfoDetail(container, itemId, labelText, info) {
        container.innerHTML = '';

        if (!info) {
            container.textContent = 'Unable to read media info.';
            return;
        }

        var sourceBps = info.VideoBitRate || info.OverallBitRate;
        var videoBitrate = formatBitrate(sourceBps);
        var audioBitrate = formatBitrate(info.AudioBitRate);

        var lines = [
            'Container: ' + (info.Container || 'unknown'),
            'Video: ' + (info.VideoCodec ? info.VideoCodec.toUpperCase() : 'none') +
                (info.Width && info.Height ? (' ' + info.Width + 'x' + info.Height) : '') +
                (videoBitrate ? (' @ ' + videoBitrate) : ' (bitrate unknown)'),
            'Audio: ' + (info.AudioCodec ? info.AudioCodec.toUpperCase() : 'none') +
                (info.AudioChannels ? (' ' + info.AudioChannels + 'ch') : '') +
                (audioBitrate ? (' @ ' + audioBitrate) : ' (bitrate unknown)'),
            'Subtitles: ' + (info.SubtitleTracks && info.SubtitleTracks.length ? info.SubtitleTracks.join(', ') : 'none'),
            'File size: ' + (formatBytes(info.FileSizeBytes) || 'unknown')
        ];

        lines.forEach(function (line) {
            var lineEl = document.createElement('div');
            lineEl.textContent = line;
            container.appendChild(lineEl);
        });

        if (sourceBps) {
            var halfKbps = Math.max(1, Math.round(sourceBps / 2 / 1000));

            var halfBitrateButton = document.createElement('button');
            halfBitrateButton.setAttribute('is', 'emby-button');
            halfBitrateButton.className = 'raised';
            halfBitrateButton.style.marginTop = '4px';
            halfBitrateButton.textContent = 'Convert at ~half bitrate (' + halfKbps + ' kbps, HEVC/AV1)';
            halfBitrateButton.addEventListener('click', function () {
                openConvertDialog([itemId], labelText, info);
                document.getElementById('codecSelect').value = 'hevc';
                document.getElementById('rateControlSelect').value = 'HalfSourceBitrate';
                updateRateControlVisibility();
            });
            container.appendChild(halfBitrateButton);
        }
    }

    // Builds a media item row (label + inline stats + a collapsible detail panel with full
    // ffprobe codec/bitrate info, toggled by clicking the label) plus the detail panel element,
    // which the caller appends as the row's sibling.
    function buildMediaRow(itemId, labelText) {
        var row = document.createElement('div');
        row.className = 'listItem';

        var label = document.createElement('span');
        label.textContent = labelText;
        label.style.cursor = 'pointer';
        label.style.textDecoration = 'underline dotted';
        label.title = 'Click for detailed codec/bitrate info';
        row.appendChild(label);

        appendStatsSpan(row, itemId);

        var detailBox = document.createElement('div');
        detailBox.style.display = 'none';
        detailBox.style.marginTop = '4px';
        detailBox.style.marginLeft = '16px';
        detailBox.style.marginBottom = '8px';
        detailBox.style.fontSize = '0.9em';
        detailBox.style.opacity = '0.85';

        label.addEventListener('click', function () {
            if (detailBox.style.display === 'none') {
                detailBox.style.display = 'block';
                detailBox.textContent = 'Loading media info…';
                apiGet('MediaConverter/Items/' + itemId + '/MediaInfo')
                    .then(function (info) {
                        renderMediaInfoDetail(detailBox, itemId, labelText, info);
                    })
                    .catch(function () {
                        detailBox.textContent = 'Unable to read media info.';
                    });
            } else {
                detailBox.style.display = 'none';
            }
        });

        return { row: row, detailBox: detailBox };
    }

    // The single item's ffprobe stats currently backing the predicted-size estimate in the
    // convert dialog; null for batch conversions or before it's loaded.
    var currentItemMediaInfo = null;

    function updateRateControlVisibility() {
        var isBitrateMode = document.getElementById('rateControlSelect').value === 'Bitrate';
        document.getElementById('videoBitrateContainer').style.display = isBitrateMode ? 'block' : 'none';
        updatePredictedSize();
    }

    function updatePredictedSize() {
        var textEl = document.getElementById('predictedSizeText');
        if (!textEl) {
            return;
        }

        var rateControlMode = document.getElementById('rateControlSelect').value;

        if (rateControlMode === 'HalfSourceBitrate') {
            if (selectedItemIds.length > 1) {
                textEl.textContent = 'Predicted size: varies per item (each is encoded at half its own source bitrate).';
                return;
            }

            if (!currentItemMediaInfo || !currentItemMediaInfo.DurationTicks) {
                textEl.textContent = 'Predicted size: unavailable (source duration unknown).';
                return;
            }

            var sourceBps = currentItemMediaInfo.VideoBitRate || currentItemMediaInfo.OverallBitRate;
            if (!sourceBps) {
                textEl.textContent = 'Predicted size: unavailable (source bitrate unknown).';
                return;
            }

            computePredictedSize(Math.round(sourceBps / 2 / 1000));
            return;
        }

        if (selectedItemIds.length !== 1) {
            textEl.textContent = 'Predicted size: only available when converting a single item.';
            return;
        }

        if (!currentItemMediaInfo || !currentItemMediaInfo.DurationTicks) {
            textEl.textContent = 'Predicted size: unavailable (source duration unknown).';
            return;
        }

        if (rateControlMode !== 'Bitrate') {
            textEl.textContent = 'Predicted size: switch rate control to "Target average bitrate" to estimate (quality-based size depends on content).';
            return;
        }

        var videoKbps = parseInt(document.getElementById('videoBitrateInput').value, 10);
        if (!(videoKbps > 0)) {
            textEl.textContent = 'Predicted size: enter a target video bitrate above.';
            return;
        }

        computePredictedSize(videoKbps);
    }

    function computePredictedSize(videoKbps) {
        var textEl = document.getElementById('predictedSizeText');
        var durationSeconds = currentItemMediaInfo.DurationTicks / 10000000;
        var audioCodec = document.getElementById('audioCodecSelect').value;
        var audioKbps = 0;
        var audioUnknown = false;

        if (audioCodec === 'copy') {
            if (currentItemMediaInfo.AudioBitRate) {
                audioKbps = currentItemMediaInfo.AudioBitRate / 1000;
            } else {
                audioUnknown = true;
            }
        } else {
            var enteredAudioKbps = parseInt(document.getElementById('audioBitrateInput').value, 10);
            if (enteredAudioKbps > 0) {
                audioKbps = enteredAudioKbps;
            } else {
                audioUnknown = true;
            }
        }

        var predictedBytes = ((videoKbps + audioKbps) * 1000 / 8) * durationSeconds;
        var sizeText = formatBytes(predictedBytes) || (predictedBytes.toFixed(0) + ' bytes');

        textEl.textContent = 'Predicted size: ~' + sizeText + (audioUnknown ? ' (audio bitrate unknown, actual size may be larger)' : '');
    }

    function openConvertDialog(itemIds, label, mediaInfo) {
        selectedItemIds = itemIds;
        var title = itemIds.length > 1 ? 'Convert ' + itemIds.length + ' items' : 'Convert';
        document.getElementById('convertDialogTitle').textContent = label ? title + ' – ' + label : title;
        document.getElementById('convertDialog').style.display = 'block';

        document.getElementById('rateControlSelect').value = 'Quality';
        document.getElementById('videoBitrateInput').value = '';
        updateRateControlVisibility();

        currentItemMediaInfo = null;

        if (itemIds.length === 1) {
            if (mediaInfo) {
                currentItemMediaInfo = mediaInfo;
                updatePredictedSize();
            } else {
                apiGet('MediaConverter/Items/' + itemIds[0] + '/MediaInfo')
                    .then(function (info) {
                        currentItemMediaInfo = info;
                        updatePredictedSize();
                    })
                    .catch(function () {
                        currentItemMediaInfo = null;
                        updatePredictedSize();
                    });
            }
        } else {
            updatePredictedSize();
        }
    }

    var targetVideoCodec = null;
    var hideOptimalItems = false;
    var lastLibraryItems = [];

    function isAlreadyOptimal(item) {
        return !!(targetVideoCodec && item.VideoCodec && item.VideoCodec.toLowerCase() === targetVideoCodec.toLowerCase());
    }

    function appendOptimalBadge(row, item) {
        if (!isAlreadyOptimal(item)) {
            return;
        }

        var badge = document.createElement('span');
        badge.style.marginLeft = '6px';
        badge.style.opacity = '0.7';
        badge.style.fontStyle = 'italic';
        badge.textContent = '(already ' + item.VideoCodec.toUpperCase() + ')';
        row.appendChild(badge);
    }

    function renderLibrary(items) {
        document.getElementById('episodesSection').style.display = 'none';
        lastLibraryItems = items;

        var container = document.getElementById('libraryResults');
        container.innerHTML = '';

        var visibleItems = hideOptimalItems ? items.filter(function (item) { return !isAlreadyOptimal(item); }) : items;

        if (!visibleItems.length) {
            var empty = document.createElement('div');
            empty.textContent = items.length ? 'No matches (all results are already in the target codec - see the toggle above).' : 'No matches.';
            container.appendChild(empty);
            return;
        }

        visibleItems.forEach(function (item) {
            var row;
            var detailBox = null;

            if (item.Type === 'Series') {
                row = document.createElement('div');
                row.className = 'listItem';

                var name = document.createElement('span');
                name.textContent = item.Name + ' (' + item.Type + ')';
                row.appendChild(name);

                var browseButton = document.createElement('button');
                browseButton.setAttribute('is', 'emby-button');
                browseButton.className = 'raised';
                browseButton.textContent = 'Browse episodes';
                browseButton.addEventListener('click', function () {
                    browseSeries(item.Id, item.Name);
                });
                row.appendChild(browseButton);

                var convertAllButton = document.createElement('button');
                convertAllButton.setAttribute('is', 'emby-button');
                convertAllButton.className = 'raised';
                convertAllButton.textContent = 'Convert all episodes';
                convertAllButton.addEventListener('click', function () {
                    apiGet('MediaConverter/Series/' + item.Id + '/Episodes')
                        .then(function (episodes) {
                            clearError();
                            if (!episodes.length) {
                                showError('Convert all episodes', { message: 'No episodes found.' });
                                return;
                            }

                            openConvertDialog(episodes.map(function (e) { return e.Id; }), item.Name + ' – all episodes (' + episodes.length + ')');
                        })
                        .catch(function (error) {
                            showError('Loading episodes', error);
                        });
                });
                row.appendChild(convertAllButton);
            } else {
                var built = buildMediaRow(item.Id, item.Name + ' (' + item.Type + ')');
                row = built.row;
                detailBox = built.detailBox;

                appendOptimalBadge(row, item);

                var button = document.createElement('button');
                button.setAttribute('is', 'emby-button');
                button.className = 'raised';
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog([item.Id], item.Name);
                });
                row.appendChild(button);
            }

            container.appendChild(row);
            if (detailBox) {
                container.appendChild(detailBox);
            }
        });
    }

    // When set, the "Back" button in the episodes section goes back one level (season list)
    // instead of hiding the whole section (back to the library search results).
    var episodesBackHandler = null;

    function setBackButtonHandler(handler, label) {
        episodesBackHandler = handler;
        var backButton = document.getElementById('backToResultsButton');
        var span = backButton.querySelector('span');
        (span || backButton).textContent = label;
    }

    function groupBySeason(episodes) {
        var seasons = {};
        var order = [];
        episodes.forEach(function (episode) {
            var seasonKey = episode.SeasonNumber != null ? episode.SeasonNumber : -1;
            if (!seasons[seasonKey]) {
                seasons[seasonKey] = [];
                order.push(seasonKey);
            }

            seasons[seasonKey].push(episode);
        });

        // Numeric ascending (Specials = season 0 first, matching Jellyfin's own convention),
        // with episodes that have no season assigned at all pushed to the very end.
        order.sort(function (a, b) {
            if (a === -1) {
                return 1;
            }

            if (b === -1) {
                return -1;
            }

            return a - b;
        });

        return { seasons: seasons, order: order };
    }

    function seasonLabel(seasonKey) {
        if (seasonKey === -1) {
            return 'No season';
        }

        return seasonKey === 0 ? 'Specials' : 'Season ' + seasonKey;
    }

    function browseSeries(seriesId, seriesName) {
        apiGet('MediaConverter/Series/' + seriesId + '/Episodes')
            .then(function (episodes) {
                clearError();
                renderSeasonList(seriesId, seriesName, episodes);
            })
            .catch(function (error) {
                showError('Loading episodes', error);
            });
    }

    function renderSeasonList(seriesId, seriesName, episodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName;
        setBackButtonHandler(null, 'Back to results');

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        if (!episodes.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No episodes found.';
            container.appendChild(empty);
            document.getElementById('episodesSection').style.display = 'block';
            return;
        }

        var convertAllRow = document.createElement('div');
        convertAllRow.className = 'listItem';
        var convertAllButton = document.createElement('button');
        convertAllButton.setAttribute('is', 'emby-button');
        convertAllButton.className = 'raised';
        convertAllButton.textContent = 'Convert all ' + episodes.length + ' episodes';
        convertAllButton.addEventListener('click', function () {
            openConvertDialog(episodes.map(function (e) { return e.Id; }), seriesName + ' – all episodes');
        });
        convertAllRow.appendChild(convertAllButton);
        container.appendChild(convertAllRow);

        var grouped = groupBySeason(episodes);

        grouped.order.forEach(function (seasonKey) {
            var seasonEpisodes = grouped.seasons[seasonKey];

            var row = document.createElement('div');
            row.className = 'listItem';

            var label = document.createElement('span');
            var seasonName = seasonLabel(seasonKey);
            label.textContent = seasonName + ' (' + seasonEpisodes.length + ' episode' + (seasonEpisodes.length === 1 ? '' : 's') + ')';
            row.appendChild(label);

            var viewButton = document.createElement('button');
            viewButton.setAttribute('is', 'emby-button');
            viewButton.className = 'raised';
            viewButton.textContent = 'View episodes';
            viewButton.addEventListener('click', function () {
                renderSeasonEpisodes(seriesId, seriesName, seasonKey, seasonName, seasonEpisodes, episodes);
            });
            row.appendChild(viewButton);

            var convertSeasonButton = document.createElement('button');
            convertSeasonButton.setAttribute('is', 'emby-button');
            convertSeasonButton.className = 'raised';
            convertSeasonButton.textContent = 'Convert this season';
            convertSeasonButton.addEventListener('click', function () {
                openConvertDialog(seasonEpisodes.map(function (e) { return e.Id; }), seriesName + ' – ' + seasonName);
            });
            row.appendChild(convertSeasonButton);

            container.appendChild(row);
        });

        document.getElementById('episodesSection').style.display = 'block';
    }

    function getSelectedEpisodeIds() {
        return Array.prototype.slice.call(document.querySelectorAll('.mcEpisodeCheckbox:checked'))
            .map(function (checkbox) {
                return checkbox.getAttribute('data-episode-id');
            });
    }

    function renderSeasonEpisodes(seriesId, seriesName, seasonKey, seasonName, seasonEpisodes, allEpisodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName + ', ' + seasonName;
        setBackButtonHandler(function () {
            renderSeasonList(seriesId, seriesName, allEpisodes);
        }, 'Back to seasons');

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        var bulkRow = document.createElement('div');
        bulkRow.style.display = 'flex';
        bulkRow.style.flexWrap = 'wrap';
        bulkRow.style.alignItems = 'center';
        bulkRow.style.gap = '8px';
        bulkRow.style.marginBottom = '8px';

        var convertSeasonButton = document.createElement('button');
        convertSeasonButton.setAttribute('is', 'emby-button');
        convertSeasonButton.className = 'raised';
        convertSeasonButton.textContent = 'Convert all ' + seasonEpisodes.length + ' episodes in this season';
        convertSeasonButton.addEventListener('click', function () {
            openConvertDialog(seasonEpisodes.map(function (e) { return e.Id; }), seriesName + ' – ' + seasonName);
        });
        bulkRow.appendChild(convertSeasonButton);

        var selectAllLabel = document.createElement('label');
        selectAllLabel.style.display = 'inline-flex';
        selectAllLabel.style.alignItems = 'center';
        selectAllLabel.style.gap = '4px';
        var selectAllCheckbox = document.createElement('input');
        selectAllCheckbox.type = 'checkbox';
        selectAllCheckbox.title = 'Select or deselect every episode below';
        selectAllCheckbox.addEventListener('change', function () {
            document.querySelectorAll('.mcEpisodeCheckbox').forEach(function (checkbox) {
                checkbox.checked = selectAllCheckbox.checked;
            });
        });
        selectAllLabel.appendChild(selectAllCheckbox);
        selectAllLabel.appendChild(document.createTextNode('Select all'));
        bulkRow.appendChild(selectAllLabel);

        var convertSelectedButton = document.createElement('button');
        convertSelectedButton.setAttribute('is', 'emby-button');
        convertSelectedButton.className = 'raised';
        convertSelectedButton.textContent = 'Convert selected episodes';
        convertSelectedButton.addEventListener('click', function () {
            var selectedIds = getSelectedEpisodeIds();
            if (!selectedIds.length) {
                showError('Convert selected episodes', { message: 'No episodes selected. Check the boxes next to the episodes you want to convert.' });
                return;
            }

            openConvertDialog(selectedIds, seriesName + ' – ' + seasonName + ' (' + selectedIds.length + ' selected)');
        });
        bulkRow.appendChild(convertSelectedButton);

        container.appendChild(bulkRow);

        seasonEpisodes.forEach(function (episode) {
            var seasonEpisode = (episode.SeasonNumber != null && episode.EpisodeNumber != null)
                ? 'S' + episode.SeasonNumber + 'E' + episode.EpisodeNumber + ' - '
                : '';
            var built = buildMediaRow(episode.Id, seasonEpisode + episode.Name);

            var checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'mcEpisodeCheckbox';
            checkbox.setAttribute('data-episode-id', episode.Id);
            checkbox.style.marginRight = '8px';
            built.row.insertBefore(checkbox, built.row.firstChild);

            appendOptimalBadge(built.row, episode);

            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.className = 'raised';
            button.textContent = 'Convert';
            button.addEventListener('click', function () {
                openConvertDialog([episode.Id], episode.Name);
            });
            built.row.appendChild(button);

            container.appendChild(built.row);
            container.appendChild(built.detailBox);
        });

        document.getElementById('episodesSection').style.display = 'block';
    }

    function buildSideBySidePlayers(job) {
        var wrapper = document.createElement('div');
        wrapper.style.display = 'flex';
        wrapper.style.flexWrap = 'wrap';
        wrapper.style.gap = '12px';
        wrapper.style.marginTop = '8px';

        function buildPlayerBlock(labelText) {
            var block = document.createElement('div');
            block.style.flex = '1 1 280px';
            block.style.maxWidth = '400px';

            var label = document.createElement('div');
            label.textContent = labelText;
            label.style.marginBottom = '4px';
            block.appendChild(label);

            var video = document.createElement('video');
            video.controls = true;
            video.preload = 'metadata';
            video.style.width = '100%';
            video.style.background = '#000';
            block.appendChild(video);

            return { block: block, video: video };
        }

        // A <video src> load can't carry the auth header ApiClient.ajax calls use, and Jellyfin's
        // "RequiresElevation" policy doesn't honor a query-string copy of that token either - so a
        // short-lived, single-purpose token is fetched first (over a normal authenticated request)
        // and used to authorize the stream instead of relying on Jellyfin's own auth for it.
        function loadStream(isVariant, video) {
            apiGet('MediaConverter/Jobs/' + job.Id + '/Stream/Token?variant=' + isVariant)
                .then(function (result) {
                    var streamPath = 'MediaConverter/Jobs/' + job.Id + '/Stream/' + (isVariant ? 'Variant' : 'Original');
                    video.src = ApiClient.getUrl(streamPath, { token: result.Token });
                    var playPromise = video.play();
                    if (playPromise && playPromise.catch) {
                        playPromise.catch(function () {});
                    }
                })
                .catch(function (error) {
                    showError('Loading preview stream', error);
                });
        }

        var originalPlayer = buildPlayerBlock('Original');
        var variantPlayer = buildPlayerBlock('New variant');

        wrapper.appendChild(originalPlayer.block);
        wrapper.appendChild(variantPlayer.block);

        loadStream(false, originalPlayer.video);
        loadStream(true, variantPlayer.video);

        function syncVariantToOriginal() {
            variantPlayer.video.currentTime = originalPlayer.video.currentTime;
        }

        // Whenever the user scrubs the original's timeline, automatically move the variant to
        // the same position - the two otherwise drift apart with no way to tell just by looking.
        originalPlayer.video.addEventListener('seeked', syncVariantToOriginal);

        var controlsRow = document.createElement('div');
        controlsRow.style.width = '100%';

        var restartButton = document.createElement('button');
        restartButton.setAttribute('is', 'emby-button');
        restartButton.className = 'raised';
        restartButton.textContent = 'Restart both & play';
        restartButton.addEventListener('click', function () {
            [originalPlayer.video, variantPlayer.video].forEach(function (video) {
                video.currentTime = 0;
            });
            [originalPlayer.video.play(), variantPlayer.video.play()].forEach(function (playPromise) {
                if (playPromise && playPromise.catch) {
                    playPromise.catch(function () {});
                }
            });
        });
        controlsRow.appendChild(restartButton);

        var syncButton = document.createElement('button');
        syncButton.setAttribute('is', 'emby-button');
        syncButton.className = 'raised';
        syncButton.textContent = 'Sync to original’s time';
        syncButton.addEventListener('click', function () {
            syncVariantToOriginal();
            if (!originalPlayer.video.paused) {
                var playPromise = variantPlayer.video.play();
                if (playPromise && playPromise.catch) {
                    playPromise.catch(function () {});
                }
            }
        });
        controlsRow.appendChild(syncButton);

        var pauseButton = document.createElement('button');
        pauseButton.setAttribute('is', 'emby-button');
        pauseButton.className = 'raised';
        pauseButton.textContent = 'Pause both';
        pauseButton.addEventListener('click', function () {
            originalPlayer.video.pause();
            variantPlayer.video.pause();
        });
        controlsRow.appendChild(pauseButton);

        wrapper.appendChild(controlsRow);

        return wrapper;
    }

    // Opens the side-by-side players in a modal overlay on top of the current page (not a new
    // browser tab/page). Clicking the backdrop outside the modal, or the Close button, pauses
    // both videos and dismisses it.
    function openSideBySideModal(job) {
        var overlay = document.createElement('div');
        overlay.style.position = 'fixed';
        overlay.style.inset = '0';
        overlay.style.background = 'rgba(0, 0, 0, 0.75)';
        overlay.style.zIndex = '10000';
        overlay.style.display = 'flex';
        overlay.style.alignItems = 'center';
        overlay.style.justifyContent = 'center';

        var modal = document.createElement('div');
        modal.style.background = '#101010';
        modal.style.borderRadius = '6px';
        modal.style.padding = '16px';
        modal.style.maxWidth = '95vw';
        modal.style.maxHeight = '90vh';
        modal.style.overflow = 'auto';

        function closeModal() {
            modal.querySelectorAll('video').forEach(function (video) {
                video.pause();
            });
            overlay.remove();
        }

        overlay.addEventListener('click', function (event) {
            if (event.target === overlay) {
                closeModal();
            }
        });

        var header = document.createElement('div');
        header.style.display = 'flex';
        header.style.justifyContent = 'space-between';
        header.style.alignItems = 'center';
        header.style.gap = '16px';
        header.style.marginBottom = '8px';

        var title = document.createElement('strong');
        title.textContent = 'Original vs. new variant';
        header.appendChild(title);

        var closeButton = document.createElement('button');
        closeButton.setAttribute('is', 'emby-button');
        closeButton.className = 'raised';
        closeButton.textContent = 'Close';
        closeButton.addEventListener('click', closeModal);
        header.appendChild(closeButton);

        var note = document.createElement('div');
        note.style.opacity = '0.7';
        note.style.fontSize = '0.85em';
        note.style.marginBottom = '8px';
        note.textContent = 'Streamed at full source quality (no re-encoding). Non-MP4 sources are repackaged ' +
            'on first play, which can take a few seconds for large files; audio codecs the browser can\'t ' +
            'decode (e.g. DTS) will be silent even though playback works.';
        modal.appendChild(header);
        modal.appendChild(note);
        modal.appendChild(buildSideBySidePlayers(job));
        overlay.appendChild(modal);
        document.body.appendChild(overlay);
    }

    function renderVariantComparison(container, job, compare) {
        container.innerHTML = '';

        var originalBlock = document.createElement('div');
        originalBlock.textContent = 'Original: ' + formatMediaInfo(compare.Original);
        container.appendChild(originalBlock);

        var variantBlock = document.createElement('div');
        variantBlock.textContent = 'New variant: ' + formatMediaInfo(compare.Variant);
        container.appendChild(variantBlock);

        var playButton = document.createElement('button');
        playButton.setAttribute('is', 'emby-button');
        playButton.className = 'raised';
        playButton.style.marginTop = '4px';
        playButton.textContent = 'Play side by side';
        playButton.addEventListener('click', function () {
            openSideBySideModal(job);
        });

        container.appendChild(playButton);

        var keepVariantButton = document.createElement('button');
        keepVariantButton.setAttribute('is', 'emby-button');
        keepVariantButton.className = 'raised';
        keepVariantButton.textContent = 'Keep new variant (delete original)';
        keepVariantButton.addEventListener('click', function () {
            if (!window.confirm('Delete the original file and keep the new variant?')) {
                return;
            }

            apiPostNoContent('MediaConverter/Jobs/' + job.Id + '/KeepVariant')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping new variant', error);
                });
        });

        var keepOriginalButton = document.createElement('button');
        keepOriginalButton.setAttribute('is', 'emby-button');
        keepOriginalButton.className = 'raised';
        keepOriginalButton.textContent = 'Keep original (delete new variant)';
        keepOriginalButton.addEventListener('click', function () {
            if (!window.confirm('Delete the new variant and keep the original file?')) {
                return;
            }

            apiPostNoContent('MediaConverter/Jobs/' + job.Id + '/KeepOriginal')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping original', error);
                });
        });

        container.appendChild(keepVariantButton);
        container.appendChild(keepOriginalButton);
    }

    function formatEta(seconds) {
        if (seconds == null) {
            return '';
        }

        var totalMinutes = Math.round(seconds / 60);
        if (totalMinutes < 1) {
            return 'less than a minute left';
        }

        if (totalMinutes < 60) {
            return '~' + totalMinutes + ' min left';
        }

        var hours = Math.floor(totalMinutes / 60);
        var minutes = totalMinutes % 60;
        return '~' + hours + 'h ' + minutes + 'm left';
    }

    function buildProgressBar(percent) {
        var track = document.createElement('div');
        track.style.display = 'inline-block';
        track.style.verticalAlign = 'middle';
        track.style.marginLeft = '8px';
        track.style.width = '160px';
        track.style.height = '8px';
        track.style.borderRadius = '4px';
        track.style.background = 'rgba(255, 255, 255, 0.15)';
        track.style.overflow = 'hidden';

        var fill = document.createElement('div');
        fill.style.height = '100%';
        fill.style.background = '#00a4dc';
        fill.style.width = Math.max(0, Math.min(100, percent)) + '%';
        fill.style.transition = 'width 0.3s ease';

        track.appendChild(fill);
        return track;
    }

    var STATUS_COLORS = {
        Queued: { fg: '#d0d0d0', bg: 'rgba(255, 255, 255, 0.1)' },
        Running: { fg: '#4fc3f7', bg: 'rgba(79, 195, 247, 0.15)' },
        Completed: { fg: '#81c784', bg: 'rgba(129, 199, 132, 0.15)' },
        Failed: { fg: '#e57373', bg: 'rgba(229, 115, 115, 0.15)' },
        Cancelled: { fg: '#ffb74d', bg: 'rgba(255, 183, 77, 0.15)' }
    };

    function buildStatusBadge(status, text) {
        var colors = STATUS_COLORS[status] || STATUS_COLORS.Queued;
        var badge = document.createElement('span');
        badge.className = 'mcStatusBadge';
        badge.style.color = colors.fg;
        badge.style.background = colors.bg;
        badge.textContent = text || status;
        return badge;
    }

    // Builds a job's table row plus its (initially hidden) expanded comparison results box, if
    // applicable. The comparison box is rendered by the caller as a separate full-width row
    // underneath, since it holds multi-line text and video players that don't fit in a cell.
    function buildJobRow(job) {
        var row = document.createElement('tr');
        var isPendingReview = job.Mode === 'Variant' && job.Status === 'Completed' && job.VariantResolution === 'PendingReview';

        var checkboxCell = document.createElement('td');
        if (isPendingReview) {
            var selectCheckbox = document.createElement('input');
            selectCheckbox.type = 'checkbox';
            selectCheckbox.className = 'mcVariantCheckbox';
            selectCheckbox.setAttribute('data-job-id', job.Id);
            selectCheckbox.title = 'Select for a bulk keep/delete action above';
            checkboxCell.appendChild(selectCheckbox);
        }
        row.appendChild(checkboxCell);

        var fileCell = document.createElement('td');
        fileCell.textContent = job.SourcePath;
        fileCell.title = job.SourcePath;
        row.appendChild(fileCell);

        var resultsBox = null;

        var statusCell = document.createElement('td');
        if (job.Status === 'Failed' && job.ErrorMessage) {
            statusCell.appendChild(buildStatusBadge('Failed', 'Failed (click for details)'));
            statusCell.style.cursor = 'pointer';
            statusCell.title = 'Click to show/hide the full error';

            resultsBox = document.createElement('pre');
            resultsBox.style.display = 'none';
            resultsBox.style.marginTop = '8px';
            resultsBox.style.maxHeight = '240px';
            resultsBox.style.overflowY = 'auto';
            resultsBox.style.whiteSpace = 'pre-wrap';
            resultsBox.style.wordBreak = 'break-word';
            resultsBox.style.background = 'rgba(255, 255, 255, 0.05)';
            resultsBox.style.padding = '8px';
            resultsBox.style.fontSize = '0.85em';
            resultsBox.textContent = job.ErrorMessage;

            statusCell.addEventListener('click', function () {
                resultsBox.style.display = resultsBox.style.display === 'none' ? 'block' : 'none';
            });
        } else {
            statusCell.appendChild(buildStatusBadge(job.Status));
        }
        row.appendChild(statusCell);

        var progressCell = document.createElement('td');
        if (job.Status === 'Running') {
            progressCell.appendChild(buildProgressBar(job.ProgressPercent));
            var pctLabel = document.createElement('span');
            pctLabel.style.marginLeft = '6px';
            var etaText = formatEta(job.EtaSeconds);
            pctLabel.textContent = Math.round(job.ProgressPercent) + '%' + (etaText ? ' (' + etaText + ')' : '');
            progressCell.appendChild(pctLabel);
        } else {
            progressCell.textContent = job.Status === 'Completed' ? '100%' : '-';
        }
        row.appendChild(progressCell);

        var actionsCell = document.createElement('td');

        if (isPendingReview) {
            var compareButton = document.createElement('button');
            compareButton.setAttribute('is', 'emby-button');
            compareButton.className = 'raised';
            compareButton.textContent = 'Compare original vs. new variant';

            resultsBox = document.createElement('div');
            resultsBox.style.display = 'none';
            resultsBox.style.marginTop = '8px';

            compareButton.addEventListener('click', function () {
                compareButton.disabled = true;
                apiGet('MediaConverter/Jobs/' + job.Id + '/Compare')
                    .then(function (compare) {
                        clearError();
                        resultsBox.style.display = 'block';
                        renderVariantComparison(resultsBox, job, compare);
                    })
                    .catch(function (error) {
                        showError('Comparing variant', error);
                    })
                    .then(function () {
                        compareButton.disabled = false;
                    });
            });

            actionsCell.appendChild(compareButton);
        }

        if (job.Status === 'Queued' || job.Status === 'Running') {
            if (job.Status === 'Queued') {
                var moveToFrontButton = document.createElement('button');
                moveToFrontButton.setAttribute('is', 'emby-button');
                moveToFrontButton.className = 'raised';
                moveToFrontButton.style.marginLeft = isPendingReview ? '8px' : '0';
                moveToFrontButton.textContent = 'Move to top';
                moveToFrontButton.addEventListener('click', function () {
                    apiPostNoContent('MediaConverter/Jobs/' + job.Id + '/MoveToFront')
                        .then(function () {
                            clearError();
                            refreshJobs();
                        })
                        .catch(function (error) {
                            showError('Reordering queue', error);
                        });
                });
                actionsCell.appendChild(moveToFrontButton);
            }

            var cancelButton = document.createElement('button');
            cancelButton.setAttribute('is', 'emby-button');
            cancelButton.className = 'raised';
            cancelButton.style.marginLeft = (isPendingReview || job.Status === 'Queued') ? '8px' : '0';
            cancelButton.textContent = 'Cancel';
            cancelButton.addEventListener('click', function () {
                apiPostNoContent('MediaConverter/Jobs/' + job.Id + '/Cancel').catch(function (error) {
                    showError('Cancelling job', error);
                });
            });
            actionsCell.appendChild(cancelButton);
        } else {
            if (job.Status === 'Failed') {
                var retryButton = document.createElement('button');
                retryButton.setAttribute('is', 'emby-button');
                retryButton.className = 'raised';
                retryButton.style.marginLeft = isPendingReview ? '8px' : '0';
                retryButton.textContent = 'Retry';
                retryButton.addEventListener('click', function () {
                    retryButton.disabled = true;
                    apiPost('MediaConverter/Jobs/' + job.Id + '/Retry')
                        .then(function () {
                            clearError();
                            refreshJobs();
                        })
                        .catch(function (error) {
                            showError('Retrying job', error);
                            retryButton.disabled = false;
                        });
                });
                actionsCell.appendChild(retryButton);
            }

            var removeButton = document.createElement('button');
            removeButton.setAttribute('is', 'emby-button');
            removeButton.className = 'raised';
            removeButton.style.marginLeft = '8px';
            removeButton.textContent = 'Remove';
            removeButton.addEventListener('click', function () {
                if (!window.confirm('Remove this job from the list? This does not delete any media files.')) {
                    return;
                }

                apiDelete('MediaConverter/Jobs/' + job.Id)
                    .then(function () {
                        clearError();
                        delete jobRenderCache[job.Id];
                        refreshJobs();
                    })
                    .catch(function (error) {
                        showError('Removing job', error);
                    });
            });
            actionsCell.appendChild(removeButton);
        }

        row.appendChild(actionsCell);

        return { row: row, resultsBox: resultsBox };
    }

    function getSelectedVariantJobIds() {
        return Array.prototype.slice.call(document.querySelectorAll('.mcVariantCheckbox:checked'))
            .map(function (checkbox) {
                return checkbox.getAttribute('data-job-id');
            });
    }

    function bulkResolveVariants(action, confirmMessage) {
        var jobIds = getSelectedVariantJobIds();
        if (!jobIds.length) {
            showError('Bulk action', { message: 'No jobs selected. Check the boxes next to the jobs you want to apply this to.' });
            return;
        }

        if (!window.confirm(confirmMessage + ' (' + jobIds.length + ' job' + (jobIds.length === 1 ? '' : 's') + ')')) {
            return;
        }

        Promise.all(jobIds.map(function (jobId) {
            return apiPostNoContent('MediaConverter/Jobs/' + jobId + '/' + action);
        })).then(function () {
            clearError();
            refreshJobs();
        }).catch(function (error) {
            showError('Bulk action', error);
            refreshJobs();
        });
    }

    // Fetches the current job list fresh (rather than trusting whatever's rendered) and removes
    // every job matching filterFn. Used for the "remove all completed"/"remove all history"
    // actions; jobs still awaiting a variant decision are always excluded by the caller's filter
    // so their file isn't orphaned with no remaining way to resolve it.
    function bulkRemoveJobs(filterFn, confirmMessage) {
        apiGet('MediaConverter/Jobs')
            .then(function (jobs) {
                var targets = jobs.filter(filterFn);
                if (!targets.length) {
                    showError('Bulk remove', { message: 'No matching jobs to remove.' });
                    return;
                }

                if (!window.confirm(confirmMessage + ' (' + targets.length + ' job' + (targets.length === 1 ? '' : 's') + ')')) {
                    return;
                }

                // allSettled (not all) so one job failing (e.g. it started running between the
                // fetch above and now) doesn't prevent every other already-eligible job from
                // being removed.
                Promise.allSettled(targets.map(function (job) {
                    return apiDelete('MediaConverter/Jobs/' + job.Id).then(function () {
                        delete jobRenderCache[job.Id];
                    });
                })).then(function (results) {
                    var failures = results.filter(function (result) { return result.status === 'rejected'; });
                    if (failures.length) {
                        showError(
                            'Bulk remove',
                            { message: 'Removed ' + (results.length - failures.length) + ' of ' + results.length + ' job(s). ' + failures.length + ' failed - see console for details.' });
                        failures.forEach(function (failure) {
                            console.error('Media Converter: Bulk remove', failure.reason);
                        });
                    } else {
                        clearError();
                    }

                    refreshJobs();
                });
            })
            .catch(function (error) {
                showError('Bulk remove', error);
            });
    }

    function isFinishedNotPending(job) {
        // VariantResolution is set to PendingReview at creation for every "create new variant" job
        // and is only ever cleared once the job actually reaches Completed and a keep/delete
        // decision is made - a job cancelled or failed before finishing stays stuck at
        // PendingReview forever with no real decision to make, so only gate on it for Completed
        // jobs specifically (matching the eligibility check the "Remove" button itself uses).
        if (job.Status === 'Completed') {
            return job.VariantResolution !== 'PendingReview';
        }

        return job.Status === 'Failed' || job.Status === 'Cancelled';
    }

    function buildBulkActionsBar() {
        var bar = document.createElement('div');

        var variantRow = document.createElement('div');
        variantRow.style.display = 'flex';
        variantRow.style.flexWrap = 'wrap';
        variantRow.style.alignItems = 'center';
        variantRow.style.gap = '8px';
        variantRow.style.marginBottom = '8px';

        var selectAllLabel = document.createElement('label');
        selectAllLabel.style.display = 'inline-flex';
        selectAllLabel.style.alignItems = 'center';
        selectAllLabel.style.gap = '4px';
        var selectAllCheckbox = document.createElement('input');
        selectAllCheckbox.type = 'checkbox';
        selectAllCheckbox.title = 'Select or deselect every pending job below';
        selectAllCheckbox.addEventListener('change', function () {
            document.querySelectorAll('.mcVariantCheckbox').forEach(function (checkbox) {
                checkbox.checked = selectAllCheckbox.checked;
            });
        });
        selectAllLabel.appendChild(selectAllCheckbox);
        selectAllLabel.appendChild(document.createTextNode('Select all pending'));
        variantRow.appendChild(selectAllLabel);

        var keepVariantButton = document.createElement('button');
        keepVariantButton.setAttribute('is', 'emby-button');
        keepVariantButton.className = 'raised';
        keepVariantButton.textContent = 'Keep selected as new variants';
        keepVariantButton.addEventListener('click', function () {
            bulkResolveVariants('KeepVariant', 'Delete the original file and keep the new variant for every selected job?');
        });
        variantRow.appendChild(keepVariantButton);

        var keepOriginalButton = document.createElement('button');
        keepOriginalButton.setAttribute('is', 'emby-button');
        keepOriginalButton.className = 'raised';
        keepOriginalButton.textContent = 'Keep selected as originals';
        keepOriginalButton.addEventListener('click', function () {
            bulkResolveVariants('KeepOriginal', 'Delete the new variant and keep the original file for every selected job?');
        });
        variantRow.appendChild(keepOriginalButton);

        bar.appendChild(variantRow);

        var historyRow = document.createElement('div');
        historyRow.style.display = 'flex';
        historyRow.style.flexWrap = 'wrap';
        historyRow.style.gap = '8px';
        historyRow.style.marginBottom = '8px';

        var removeCompletedButton = document.createElement('button');
        removeCompletedButton.setAttribute('is', 'emby-button');
        removeCompletedButton.className = 'raised';
        removeCompletedButton.textContent = 'Remove all completed jobs';
        removeCompletedButton.addEventListener('click', function () {
            bulkRemoveJobs(
                function (job) { return job.Status === 'Completed' && job.VariantResolution !== 'PendingReview'; },
                'Remove all completed jobs from the list? Jobs still awaiting a variant keep/delete decision are left alone. This does not delete any media files.');
        });
        historyRow.appendChild(removeCompletedButton);

        var removeHistoryButton = document.createElement('button');
        removeHistoryButton.setAttribute('is', 'emby-button');
        removeHistoryButton.className = 'raised';
        removeHistoryButton.textContent = 'Remove all history';
        removeHistoryButton.addEventListener('click', function () {
            bulkRemoveJobs(
                isFinishedNotPending,
                'Remove all finished jobs (completed, failed, and cancelled) from the list? Jobs still awaiting a variant keep/delete decision are left alone. This does not delete any media files.');
        });
        historyRow.appendChild(removeHistoryButton);

        bar.appendChild(historyRow);

        return bar;
    }

    function jobSignature(job) {
        return [job.Status, Math.round(job.ProgressPercent), job.VariantResolution].join('|');
    }

    // Keyed by job id: { signature, row, panelRow }. Reused across polls so that a job whose data
    // hasn't changed (e.g. a completed job awaiting a variant decision) keeps its existing DOM -
    // rebuilding it every poll was wiping out the comparison results panel and would also reset
    // any embedded <video> players.
    var jobRenderCache = {};

    var JOBS_TABLE_COLUMN_COUNT = 5;

    function renderJobs(jobs) {
        var container = document.getElementById('jobResults');
        var seenIds = {};

        jobs.forEach(function (job) {
            seenIds[job.Id] = true;
            var signature = jobSignature(job);
            var cached = jobRenderCache[job.Id];

            if (!cached || cached.signature !== signature) {
                if (cached) {
                    cached.row.remove();
                    if (cached.panelRow) {
                        cached.panelRow.remove();
                    }
                }

                var built = buildJobRow(job);
                var panelRow = null;

                if (built.resultsBox) {
                    panelRow = document.createElement('tr');
                    var panelCell = document.createElement('td');
                    panelCell.colSpan = JOBS_TABLE_COLUMN_COUNT;
                    panelCell.appendChild(built.resultsBox);
                    panelRow.appendChild(panelCell);
                }

                cached = { signature: signature, row: built.row, panelRow: panelRow };
                jobRenderCache[job.Id] = cached;
            }

            container.appendChild(cached.row);
            if (cached.panelRow) {
                container.appendChild(cached.panelRow);
            }
        });

        Object.keys(jobRenderCache).forEach(function (id) {
            if (!seenIds[id]) {
                jobRenderCache[id].row.remove();
                if (jobRenderCache[id].panelRow) {
                    jobRenderCache[id].panelRow.remove();
                }
                delete jobRenderCache[id];
            }
        });
    }

    var queuePaused = false;

    function updateQueuePauseUi() {
        var button = document.getElementById('queuePauseToggleButton');
        if (!button) {
            return;
        }

        var span = button.querySelector('span');
        (span || button).textContent = queuePaused ? 'Resume queue' : 'Pause queue after current job';

        var statusText = document.getElementById('queuePauseStatusText');
        if (statusText) {
            statusText.textContent = queuePaused
                ? 'Paused - the current job (if any) will finish, then no further jobs will start.'
                : '';
        }
    }

    function refreshQueuePausedState() {
        apiGet('MediaConverter/Queue/Paused')
            .then(function (result) {
                queuePaused = !!result.Paused;
                updateQueuePauseUi();
            })
            .catch(function () {});
    }

    var SVG_NS = 'http://www.w3.org/2000/svg';

    // A donut chart showing what percent of the original size was saved, using the classic
    // stroke-dasharray trick: a circle with r=15.915 has a circumference of ~100, so the dash
    // array can be expressed directly as "percent, 100-percent".
    function buildSavedDonut(percentSaved) {
        var pct = Math.max(0, Math.min(100, percentSaved));

        var svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('viewBox', '0 0 36 36');

        var track = document.createElementNS(SVG_NS, 'circle');
        track.setAttribute('cx', '18');
        track.setAttribute('cy', '18');
        track.setAttribute('r', '15.915');
        track.setAttribute('fill', 'none');
        track.setAttribute('stroke', 'rgba(255, 255, 255, 0.12)');
        track.setAttribute('stroke-width', '4');
        svg.appendChild(track);

        var arc = document.createElementNS(SVG_NS, 'circle');
        arc.setAttribute('cx', '18');
        arc.setAttribute('cy', '18');
        arc.setAttribute('r', '15.915');
        arc.setAttribute('fill', 'none');
        arc.setAttribute('stroke', '#81c784');
        arc.setAttribute('stroke-width', '4');
        arc.setAttribute('stroke-linecap', 'round');
        arc.setAttribute('stroke-dasharray', pct + ' ' + (100 - pct));
        arc.setAttribute('transform', 'rotate(-90 18 18)');
        svg.appendChild(arc);

        var label = document.createElementNS(SVG_NS, 'text');
        label.setAttribute('x', '18');
        label.setAttribute('y', '20.8');
        label.setAttribute('text-anchor', 'middle');
        label.setAttribute('font-size', '8.5');
        label.setAttribute('fill', 'currentColor');
        label.textContent = Math.round(pct) + '%';
        svg.appendChild(label);

        return svg;
    }

    function refreshStats() {
        var bar = document.getElementById('statsBar');
        if (!bar) {
            return;
        }

        apiGet('MediaConverter/Stats')
            .then(function (stats) {
                bar.innerHTML = '';

                if (!stats.CompletedJobCount) {
                    return;
                }

                var percentSaved = stats.TotalSourceBytes > 0
                    ? (stats.TotalSavedBytes / stats.TotalSourceBytes) * 100
                    : 0;

                var card = document.createElement('div');
                card.className = 'mcStatCard';
                card.appendChild(buildSavedDonut(percentSaved));

                var text = document.createElement('div');
                text.className = 'mcStatText';

                var savedText = formatBytes(stats.TotalSavedBytes) || '0 MB';
                var line1 = document.createElement('div');
                line1.innerHTML = '<strong>' + savedText + '</strong> saved so far across ' +
                    stats.CompletedJobCount + ' job' + (stats.CompletedJobCount === 1 ? '' : 's') + '.';
                text.appendChild(line1);

                if (stats.AverageCompressionRatio != null) {
                    var line2 = document.createElement('div');
                    line2.textContent = 'Output files average ' + Math.round(stats.AverageCompressionRatio * 100) +
                        '% of their original size.';
                    text.appendChild(line2);
                }

                card.appendChild(text);
                bar.appendChild(card);
            })
            .catch(function () {
                bar.innerHTML = '';
            });
    }

    function refreshJobs() {
        apiGet('MediaConverter/Jobs')
            .then(renderJobs)
            .catch(function (error) {
                showError('Loading jobs', error);
            });
        refreshQueuePausedState();
        refreshStats();
    }

    function search() {
        var searchTerm = document.getElementById('searchTerm').value || '';
        apiGet('MediaConverter/Library?searchTerm=' + encodeURIComponent(searchTerm))
            .then(function (items) {
                clearError();
                renderLibrary(items);
            })
            .catch(function (error) {
                showError('Searching library', error);
            });
    }

    function renderRules(rules) {
        var container = document.getElementById('rulesResults');
        container.innerHTML = '';

        if (!rules.length) {
            var empty = document.createElement('div');
            empty.style.opacity = '0.7';
            empty.textContent = 'No rules yet.';
            container.appendChild(empty);
            return;
        }

        rules.forEach(function (rule) {
            var row = document.createElement('div');
            row.className = 'listItem';

            var label = document.createElement('span');
            label.textContent = rule.Name + ' - ' + rule.VideoCodec.toUpperCase() + '/' + rule.Container +
                ', quality ' + rule.Quality + ', ' + (rule.Mode === 'Replace' ? 'replace original' : 'new variant') +
                (rule.PathContains ? (', path contains "' + rule.PathContains + '"') : '') +
                (rule.Enabled ? '' : ' (disabled)');
            label.style.opacity = rule.Enabled ? '1' : '0.6';
            row.appendChild(label);

            var toggleButton = document.createElement('button');
            toggleButton.setAttribute('is', 'emby-button');
            toggleButton.className = 'raised';
            toggleButton.style.marginLeft = '8px';
            toggleButton.textContent = rule.Enabled ? 'Disable' : 'Enable';
            toggleButton.addEventListener('click', function () {
                apiPut('MediaConverter/Rules/' + rule.Id, ruleToRequestBody(rule, { Enabled: !rule.Enabled }))
                    .then(function () {
                        clearError();
                        refreshRules();
                    })
                    .catch(function (error) {
                        showError('Updating rule', error);
                    });
            });
            row.appendChild(toggleButton);

            var deleteButton = document.createElement('button');
            deleteButton.setAttribute('is', 'emby-button');
            deleteButton.className = 'raised';
            deleteButton.style.marginLeft = '8px';
            deleteButton.textContent = 'Delete';
            deleteButton.addEventListener('click', function () {
                if (!window.confirm('Delete the rule "' + rule.Name + '"? This does not affect jobs already queued or converted.')) {
                    return;
                }

                apiDelete('MediaConverter/Rules/' + rule.Id)
                    .then(function () {
                        clearError();
                        refreshRules();
                    })
                    .catch(function (error) {
                        showError('Deleting rule', error);
                    });
            });
            row.appendChild(deleteButton);

            container.appendChild(row);
        });
    }

    function ruleToRequestBody(rule, overrides) {
        return Object.assign({
            Name: rule.Name,
            Enabled: rule.Enabled,
            Container: rule.Container,
            VideoCodec: rule.VideoCodec,
            Quality: rule.Quality,
            Mode: rule.Mode,
            PathContains: rule.PathContains
        }, overrides || {});
    }

    function refreshRules() {
        apiGet('MediaConverter/Rules')
            .then(function (rules) {
                renderRules(rules);
            })
            .catch(function (error) {
                showError('Loading rules', error);
            });
    }

    function init() {
        document.getElementById('jobsBulkActions').appendChild(buildBulkActionsBar());

        refreshRules();

        document.getElementById('addRuleButton').addEventListener('click', function () {
            var name = document.getElementById('ruleNameInput').value.trim();
            if (!name) {
                showError('Add rule', { message: 'Enter a rule name.' });
                return;
            }

            apiPost('MediaConverter/Rules', {
                Name: name,
                Enabled: true,
                Container: document.getElementById('ruleContainerSelect').value,
                VideoCodec: document.getElementById('ruleCodecSelect').value,
                Quality: parseInt(document.getElementById('ruleQualityInput').value, 10) || 23,
                Mode: document.getElementById('ruleModeSelect').value,
                PathContains: document.getElementById('rulePathContainsInput').value.trim() || null
            }).then(function () {
                clearError();
                document.getElementById('ruleNameInput').value = '';
                document.getElementById('rulePathContainsInput').value = '';
                refreshRules();
            }).catch(function (error) {
                showError('Adding rule', error);
            });
        });

        document.getElementById('runRulesNowButton').addEventListener('click', function () {
            var button = document.getElementById('runRulesNowButton');
            button.disabled = true;
            var originalText = button.querySelector('span').textContent;
            button.querySelector('span').textContent = 'Scanning… (check Jobs below shortly)';

            apiPostNoContent('MediaConverter/Rules/RunNow')
                .then(function () {
                    clearError();
                })
                .catch(function (error) {
                    showError('Running rules', error);
                })
                .then(function () {
                    setTimeout(function () {
                        button.disabled = false;
                        button.querySelector('span').textContent = originalText;
                    }, 3000);
                });
        });

        apiGet('MediaConverter/Config/DefaultVideoCodec')
            .then(function (codec) {
                targetVideoCodec = codec;
            })
            .catch(function () {});

        document.getElementById('hideOptimalCheckbox').addEventListener('change', function (event) {
            hideOptimalItems = event.target.checked;
            renderLibrary(lastLibraryItems);
        });

        document.getElementById('queuePauseToggleButton').addEventListener('click', function () {
            var newState = !queuePaused;
            apiPostNoContent('MediaConverter/Queue/Paused?paused=' + newState)
                .then(function () {
                    clearError();
                    queuePaused = newState;
                    updateQueuePauseUi();
                })
                .catch(function (error) {
                    showError('Updating queue pause state', error);
                });
        });

        document.getElementById('searchButton').addEventListener('click', search);

        document.getElementById('backToResultsButton').addEventListener('click', function () {
            if (episodesBackHandler) {
                episodesBackHandler();
            } else {
                document.getElementById('episodesSection').style.display = 'none';
            }
        });

        document.getElementById('rateControlSelect').addEventListener('change', updateRateControlVisibility);
        document.getElementById('videoBitrateInput').addEventListener('input', updatePredictedSize);
        document.getElementById('audioCodecSelect').addEventListener('change', updatePredictedSize);
        document.getElementById('audioBitrateInput').addEventListener('input', updatePredictedSize);

        document.getElementById('startConversionButton').addEventListener('click', function () {
            if (!selectedItemIds.length) {
                return;
            }

            var audioBitrateValue = document.getElementById('audioBitrateInput').value;
            var resolutionValue = document.getElementById('resolutionSelect').value;
            var videoBitrateValue = document.getElementById('videoBitrateInput').value;
            var maxVideoBitrateValue = document.getElementById('maxVideoBitrateInput').value;

            apiPost('MediaConverter/Convert/Batch', {
                ItemIds: selectedItemIds,
                Container: document.getElementById('containerSelect').value,
                VideoCodec: document.getElementById('codecSelect').value,
                Quality: parseInt(document.getElementById('qualityInput').value, 10),
                RateControlMode: document.getElementById('rateControlSelect').value,
                VideoBitrateKbps: videoBitrateValue ? parseInt(videoBitrateValue, 10) : null,
                MaxVideoBitrateKbps: maxVideoBitrateValue ? parseInt(maxVideoBitrateValue, 10) : null,
                Mode: document.getElementById('modeSelect').value,
                Preset: document.getElementById('presetSelect').value || null,
                ScaleHeight: resolutionValue ? parseInt(resolutionValue, 10) : null,
                AudioCodec: document.getElementById('audioCodecSelect').value,
                AudioBitrateKbps: audioBitrateValue ? parseInt(audioBitrateValue, 10) : null,
                SubtitleMode: document.getElementById('subtitleModeSelect').value,
                FfmpegArgsOverride: document.getElementById('ffmpegOverrideInput').value || null
            }).then(function () {
                clearError();
                document.getElementById('convertDialog').style.display = 'none';
                refreshJobs();
            }).catch(function (error) {
                showError('Starting conversion', error);
            });
        });

        refreshJobs();
        setInterval(refreshJobs, 3000);
    }

    init();

    // Belt-and-braces: some Jellyfin web client versions re-fire this custom event
    // on subsequent navigations to the (cached) page without re-running its scripts.
    document.addEventListener('pageshow', refreshJobs);
})();
