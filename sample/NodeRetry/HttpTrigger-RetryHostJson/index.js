var invocationCount = 0;

module.exports = async function (context, req) {
    if (context.executionContext.retryCount !== invocationCount) {
        context.res = {
            status: 500,
            body: "executionContext.retryCount=" + context.retryCount + " is not equal to invocationCount=" + invocationCount
        };
    } else if (context.executionContext.maxRetryCount !== 2) {
        context.res = {
            status: 500,
            body: "executionContext.maxRetryCount=" + context.maxRetryCount + " is not equal to 4" + invocationCount
        };
    } else {
        const reset = req.query.reset;
        invocationCount = reset ? 0 : invocationCount

        context.log('JavaScript HTTP trigger function processed a request.invocationCount: ' + invocationCount);

        invocationCount = invocationCount + 1;
        const responseMessage = "invocationCount: " + invocationCount;
        if (invocationCount < 2) {
            throw new Error('An error occurred');
        }
        context.res = {
            // status: 200, /* Defaults to 200 */
            body: responseMessage
        };
    }
}