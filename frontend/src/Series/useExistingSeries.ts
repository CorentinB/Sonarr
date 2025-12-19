import { useMemo } from 'react';
import Series from 'Series/Series';
import useSeries from 'Series/useSeries';

function useExistingSeries(
  tvdbId: number | undefined,
  tmdbId: number | undefined
): boolean {
  const existingSeries = useFindExistingSeries(tvdbId, tmdbId);
  return existingSeries != null;
}

export function useFindExistingSeries(
  tvdbId: number | undefined,
  tmdbId: number | undefined
): Series | undefined {
  const { data: series = [] } = useSeries();

  return useMemo(() => {
    // Check by tmdbId first (for TMDB search results)
    if (tmdbId != null && tmdbId > 0) {
      return series.find((s) => s.tmdbId === tmdbId);
    }

    // Fall back to tvdbId
    if (tvdbId != null && tvdbId > 0) {
      return series.find((s) => s.tvdbId === tvdbId);
    }

    return undefined;
  }, [tvdbId, tmdbId, series]);
}

export default useExistingSeries;
