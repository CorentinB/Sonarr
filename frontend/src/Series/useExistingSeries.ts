import { useMemo } from 'react';
import useSeries from 'Series/useSeries';

function useExistingSeries(
  tvdbId: number | undefined,
  tmdbId: number | undefined
) {
  const { data: series = [] } = useSeries();

  return useMemo(() => {
    // Check by tmdbId first (for TMDB search results)
    if (tmdbId != null && tmdbId > 0) {
      return series.some((s) => s.tmdbId === tmdbId);
    }

    // Fall back to tvdbId
    if (tvdbId != null && tvdbId > 0) {
      return series.some((s) => s.tvdbId === tvdbId);
    }

    return false;
  }, [tvdbId, tmdbId, series]);
}

export default useExistingSeries;
