import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set } from './baseActions';

//
// Variables

export const section = 'diagnostic';
//
// State

const exampleScript = 'var seriesService = Resolve<ISeriesService>();\r\nreturn seriesService.GetAllSeries().Count();';

export const defaultState = {
  status: {
    isFetching: false,
    isPopulated: false,
    error: null,
    item: {}
  }
};

//
// Actions Types

export const FETCH_STATUS = 'diagnostic/status/fetchStatus';

//
// Action Creators

export const fetchStatus = createThunk(FETCH_STATUS);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_STATUS]: createFetchHandler('diagnostic.status', '/diagnostic/status')

});

//
// Reducers

export const reducers = createHandleActions({

}, defaultState, section);
