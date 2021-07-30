# Contains patterns to match errors in the hoarder json suites.failureReasons in case of non-test related errors
#
# Conclusion can be either: success, failure, cancelled, inconclusive.
# Conclusions of hoarder_log_patterns overwrite conclusions of execution_log_patterns.
# Tags of hoarder_log_patterns get appended to tags of execution_log_patterns.

hoarder_log_patterns = [
    {
        'pattern': r'System.TimeoutException: Timeout while waiting for a first message',
        'tags': ['System.TimeoutException'],
        'conclusion': 'failure',
    },
    {
        # this matches everything and must therefore be the last item in the list
        'pattern': r'.+',
        'tags': ['unknown'],
        'conclusion': 'failure',
    }

]
